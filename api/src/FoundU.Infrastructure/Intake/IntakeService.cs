using FoundU.Application.Intake;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Intake;

/// <summary>
/// Read-only intake. Existing report/recognition/claim endpoints own all writes.
///
/// Two conversations share it. An owner ("I lost...") is searched against found items; a
/// finder ("I found...") against open lost reports, so the item can go straight to the person
/// looking for it. A finder is only ever shown what the public feed already shows.
/// </summary>
public sealed class IntakeService(FoundUDbContext db, IIntakeAgentClient agent) : IIntakeService
{
    public async Task<IntakeResponse> AskAsync(IntakeRequest request, Guid studentId, CancellationToken cancellationToken = default)
    {
        var types = await db.ItemTypes.AsNoTracking()
            .Where(t => t.IsActive && t.Category.IsActive).OrderBy(t => t.Name).Take(200)
            .Select(t => new { t.Id, t.CategoryId, t.Name }).ToListAsync(cancellationToken);
        var locations = await db.CampusLocations.AsNoTracking().Where(l => l.IsActive)
            .OrderBy(l => l.Name).Take(200).Select(l => new { l.Id, l.Name }).ToListAsync(cancellationToken);
        var vocabulary = new IntakeVocabulary(types.Select(t => t.Name).ToList(), locations.Select(l => l.Name).ToList());

        IntakeSlots Canonical(IntakeSlots slots) => slots with
        {
            ItemType = types.FirstOrDefault(t => string.Equals(t.Name, slots.ItemType?.Trim(), StringComparison.OrdinalIgnoreCase))?.Name,
            Location = locations.FirstOrDefault(l => string.Equals(l.Name, slots.Location?.Trim(), StringComparison.OrdinalIgnoreCase))?.Name,
            Colour = Clean(slots.Colour), When = Clean(slots.When), Distinctive = Clean(slots.Distinctive),
            Intent = Clean(slots.Intent)?.ToLowerInvariant() is { } intent && IntakeSlots.IsValidIntent(intent) ? intent : null,
        };

        var input = Canonical(request.Slots ?? new());
        var extraction = await agent.RunAsync(new([new("user", request.Message.Trim())], input, vocabulary), cancellationToken);
        var slots = Canonical(extraction?.Slots ?? input);
        // The agent settles the side once; if it said nothing, what the page already knew stands.
        if (slots.Intent is null && input.Intent is not null) slots = slots with { Intent = input.Intent };
        var itemType = types.FirstOrDefault(t => t.Name == slots.ItemType);
        var location = locations.FirstOrDefault(l => l.Name == slots.Location);

        if (slots.Intent == IntakeSlots.Found)
            return await AskAsFinderAsync(request, studentId, slots, vocabulary, extraction,
                itemType is null ? null : (itemType.Id, itemType.CategoryId, itemType.Name),
                location is null ? null : (location.Id, location.Name), cancellationToken);

        // The draft is a sentence the student will see in the report form, so it reads like one:
        // their own words when nothing was recognised, otherwise plain lower-case prose.
        var description = itemType is null ? request.Message.Trim()
            : $"I lost my {slots.Colour?.ToLowerInvariant()} {itemType.Name.ToLowerInvariant()}".Replace("  ", " ")
              + (location is null ? "." : $". Last seen at {location.Name}.")
              + (slots.Distinctive is null ? "" : $" {slots.Distinctive}");
        var draft = new IntakeDraft(itemType?.CategoryId, itemType?.Id, location?.Id, description, slots.Colour, slots.When);

        IntakeResponse Unavailable() => new("unavailable",
            "The assistant is unavailable right now. You can still review a draft and report your lost item.", slots, draft);
        if (extraction is null) return Unavailable();
        if (itemType is null)
            return new("collecting", slots.Intent is null
                ? "Did you lose something, or find something? Tell me what it is - a backpack, a water bottle, a student ID card."
                : "What did you lose? Tell me the kind of item, such as a backpack, water bottle or student ID card.", slots, draft);
        if (slots.Colour is null && location is null)
            return new("collecting", $"What colour is your {itemType.Name.ToLowerInvariant()}, or where did you last see it?", slots, draft);

        var colour = slots.Colour?.ToLowerInvariant();
        var locationId = location?.Id;
        // No hidden evidence, owner details, photos or collection/hand-in codes cross this boundary.
        // A candidate must match the item type and at least one additional search hint.
        var candidates = await db.FoundReports.AsNoTracking()
            .Where(f => (f.Status == FoundReportStatus.Unclaimed || f.Status == FoundReportStatus.Posted)
                && f.FinderId != studentId && f.ItemTypeId == itemType.Id
                && ((colour != null && f.PrimaryColor != null && f.PrimaryColor.ToLower() == colour)
                    || (locationId != null && f.FoundLocationId == locationId)))
            .OrderByDescending(f => colour != null && f.PrimaryColor != null && f.PrimaryColor.ToLower() == colour)
            .ThenByDescending(f => locationId != null && f.FoundLocationId == locationId)
            .ThenByDescending(f => f.FoundAt).Take(20)
            .Select(f => new IntakeMatch(f.Id, f.Status == FoundReportStatus.Posted ? "post" : "desk",
                f.ItemType.Name, f.PrimaryColor, f.FoundLocation.Name,
                f.GeneralDescription.Length > 500 ? f.GeneralDescription.Substring(0, 500) : f.GeneralDescription))
            .ToListAsync(cancellationToken);
        var result = await agent.RunAsync(new([new("assistant", "Search the available candidates.")], slots, vocabulary, candidates), cancellationToken);
        if (result is null) return Unavailable();
        var match = result.Phase == "matched" && Guid.TryParse(result.MatchCandidateId, out var id)
            ? candidates.FirstOrDefault(c => c.Id == id) : null;
        if (result.Phase == "matched" && match is null) return Unavailable();
        if (match is null)
            return new("no_match", "I couldn't find a close match in the available items. Review your draft report and check the details before posting it.", slots, draft);
        return new("matched", match.Kind == "desk"
            ? "This might be yours. It is at a desk. If you recognise it, choose your lost report and open a claim; staff will verify ownership."
            : "This might be yours. The finder has not handed it in yet. If you recognise it, choose your lost report to ask the finder to hand it in.", slots, draft, match);
    }

    /// <summary>
    /// A finder holding someone else's item. Searches what people have reported lost - the
    /// same reports, and the same fields, the public feed shows - and never the finder's own.
    /// </summary>
    private async Task<IntakeResponse> AskAsFinderAsync(
        IntakeRequest request,
        Guid studentId,
        IntakeSlots slots,
        IntakeVocabulary vocabulary,
        IntakeAgentResult? extraction,
        (Guid Id, Guid CategoryId, string Name)? itemType,
        (Guid Id, string Name)? location,
        CancellationToken cancellationToken)
    {
        // "blue water bottle" - what the replies call it, and the start of the draft.
        var what = itemType is null ? "" : $"{slots.Colour?.ToLowerInvariant()} {itemType.Value.Name.ToLowerInvariant()}".Trim();
        // The draft becomes a found post the finder reviews before anything is published.
        var description = itemType is null ? request.Message.Trim()
            : char.ToUpperInvariant(what[0]) + what[1..]
              + (location is null ? "." : $", found at {location.Value.Name}.")
              + (slots.Distinctive is null ? "" : $" {slots.Distinctive}");
        var draft = new IntakeDraft(itemType?.CategoryId, itemType?.Id, location?.Id, description, slots.Colour, slots.When);
        var careNote = itemType is null ? "" : await ValuablesNoteAsync(itemType.Value.Id, cancellationToken);

        if (extraction is null)
            return new("unavailable",
                "The assistant is unavailable right now. You can still post what you found, or hand it in at any security desk." + careNote,
                slots, draft);
        if (itemType is null)
            return new("collecting",
                "Thanks for picking it up. What did you find? Tell me the kind of item, such as a backpack, water bottle or student ID card.",
                slots, draft);
        if (slots.Colour is null && location is null)
            return new("collecting",
                $"What colour is the {itemType.Value.Name.ToLowerInvariant()}, or where did you find it?", slots, draft);

        var colour = slots.Colour?.ToLowerInvariant();
        var locationId = location?.Id;
        var typeId = itemType.Value.Id;
        var now = DateTime.UtcNow;
        // Open, unpaused reports only - the feed's own rule. A paused report already has a
        // finder walking its item to a desk.
        var candidates = await db.LostReports.AsNoTracking()
            .Where(r => r.Status == LostReportStatus.Active
                && (r.PausedUntil == null || r.PausedUntil < now)
                && r.StudentId != studentId && r.ItemTypeId == typeId
                && ((colour != null && r.PrimaryColor != null && r.PrimaryColor.ToLower() == colour)
                    || (locationId != null && r.LastSeenLocationId == locationId)))
            .OrderByDescending(r => colour != null && r.PrimaryColor != null && r.PrimaryColor.ToLower() == colour)
            .ThenByDescending(r => locationId != null && r.LastSeenLocationId == locationId)
            .ThenByDescending(r => r.CreatedAt).Take(20)
            .Select(r => new IntakeMatch(r.Id, "lost", r.ItemType.Name, r.PrimaryColor, r.LastSeenLocation.Name,
                r.Description.Length > 500 ? r.Description.Substring(0, 500) : r.Description))
            .ToListAsync(cancellationToken);

        var result = await agent.RunAsync(new([new("assistant", "Search the available candidates.")], slots, vocabulary, candidates), cancellationToken);
        if (result is null)
            return new("unavailable",
                "The assistant is unavailable right now. You can still post what you found, or hand it in at any security desk." + careNote,
                slots, draft);
        var match = result.Phase == "matched" && Guid.TryParse(result.MatchCandidateId, out var id)
            ? candidates.FirstOrDefault(c => c.Id == id) : null;
        if (match is null)
            return new("no_match",
                $"Nobody has reported a {what} lost yet. Post it as a found item so the owner can spot it, or hand it in at any security desk." + careNote,
                slots, draft);
        return new("matched",
            "Someone is looking for this. If it's what you have, open their report and press I found this - you can message them, or hand it to security and they get a code to collect it." + careNote,
            slots, draft, match);
    }

    /// <summary>
    /// Wallets, phones, cards and keys are safer at a desk than in a stranger's bag. Keyed off
    /// the campus's own names, so a category an admin adds later is covered by its wording.
    /// </summary>
    private async Task<string> ValuablesNoteAsync(Guid itemTypeId, CancellationToken cancellationToken)
    {
        var names = await db.ItemTypes.AsNoTracking().Where(t => t.Id == itemTypeId)
            .Select(t => t.Name + " " + t.Category.Name).FirstOrDefaultAsync(cancellationToken) ?? "";
        string[] valuable = ["wallet", "purse", "phone", "laptop", "tablet", "card", "id", "licence", "license", "passport", "key", "electronic", "cash"];
        var words = names.ToLowerInvariant().Split([' ', '&', '/', ','], StringSplitOptions.RemoveEmptyEntries);
        return words.Any(w => valuable.Any(v => w.StartsWith(v, StringComparison.Ordinal)))
            ? " Because it's the kind of thing people carry money or ID in, handing it to security is the safest choice."
            : "";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
