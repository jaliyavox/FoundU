using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// The lists everyone picks from - categories, item types, campus places, storage - for admins
/// to look after. Retire what is in use; delete only what nothing names.
///
/// The kind is the route segment: categories, item-types, locations or storage.
/// </summary>
[ApiController]
[Route("api/admin/reference")]
[Authorize(Policy = PolicyNames.Admin)]
public class AdminReferenceController : ControllerBase
{
    private readonly IReferenceAdminService _reference;

    public AdminReferenceController(IReferenceAdminService reference)
    {
        _reference = reference;
    }

    /// <summary>Everything, retired rows included, with how many records name each.</summary>
    [HttpGet]
    public async Task<ActionResult<ReferenceAdminDto>> All(CancellationToken cancellationToken)
        => Ok(await _reference.GetAllAsync(cancellationToken));

    [HttpPost("{kind}")]
    public async Task<ActionResult<object>> Create(
        string kind,
        [FromBody] ReferenceItemRequest request,
        CancellationToken cancellationToken)
        => Ok(new { id = await _reference.CreateAsync(Parse(kind), request, cancellationToken) });

    [HttpPut("{kind}/{id:guid}")]
    public async Task<IActionResult> Update(
        string kind,
        Guid id,
        [FromBody] ReferenceItemRequest request,
        CancellationToken cancellationToken)
    {
        await _reference.UpdateAsync(Parse(kind), id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Retire (false) or restore (true).</summary>
    [HttpPost("{kind}/{id:guid}/active")]
    public async Task<IActionResult> SetActive(
        string kind,
        Guid id,
        [FromBody] SetReferenceActiveRequest request,
        CancellationToken cancellationToken)
    {
        await _reference.SetActiveAsync(Parse(kind), id, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{kind}/{id:guid}")]
    public async Task<IActionResult> Delete(string kind, Guid id, CancellationToken cancellationToken)
    {
        await _reference.DeleteAsync(Parse(kind), id, cancellationToken);
        return NoContent();
    }

    private static ReferenceKind Parse(string kind) => kind.ToLowerInvariant() switch
    {
        "categories" => ReferenceKind.Categories,
        "item-types" => ReferenceKind.ItemTypes,
        "locations" => ReferenceKind.Locations,
        "storage" => ReferenceKind.Storage,
        _ => throw new FoundU.Application.Common.Exceptions.NotFoundAppException($"Unknown kind '{kind}'."),
    };
}
