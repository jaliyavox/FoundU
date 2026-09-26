using FoundU.Application.Intake;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Intake;

/// <summary>Read-only intake. Existing report/recognition/claim endpoints own all writes.</summary>
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
        };

        var input = Canonical(request.Slots ?? new());
        var extraction = await agent.RunAsync(new([new("user", request.Message.Trim())], input, vocabulary), cancellationToken);
        var slots = Canonical(extraction?.Slots ?? input);
        var itemType = types.FirstOrDefault(t => t.Name == slots.ItemType);
        var location = locations.FirstOrDefault(l => l.Name == slots.Location);
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
            return new("collecting", "What did you lose? Tell me the kind of item, such as a backpack, water bottle or student ID card.", slots, draft);
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

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
