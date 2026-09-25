using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Reference.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>Lookup data that populates the report forms. Read-only; managed by Admin in Step 9.</summary>
[ApiController]
[Route("api/reference")]
[Authorize]
public class ReferenceController : ControllerBase
{
    private readonly IReferenceDataService _referenceData;

    public ReferenceController(IReferenceDataService referenceData)
    {
        _referenceData = referenceData;
    }

    /// <summary>Active categories, each with its active item types nested.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
        => Ok(await _referenceData.GetCategoriesAsync(cancellationToken));

    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<CampusLocationDto>>> GetLocations(CancellationToken cancellationToken)
        => Ok(await _referenceData.GetCampusLocationsAsync(cancellationToken));

    /// <summary>Staff-only: students never choose where an item is stored.</summary>
    [HttpGet("storage-locations")]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<IReadOnlyList<StorageLocationDto>>> GetStorageLocations(CancellationToken cancellationToken)
        => Ok(await _referenceData.GetStorageLocationsAsync(cancellationToken));
}
