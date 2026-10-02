using FoundU.Application.Auth;
using FoundU.Application.Desk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// "A finder is handing something in": whatever code they quote, this says what it is and
/// where the desk deals with it. Staff only; read-only.
/// </summary>
[ApiController]
[Route("api/desk/codes")]
[Authorize(Policy = PolicyNames.Staff)]
public class DeskCodesController : ControllerBase
{
    private readonly IDeskCodeService _codes;

    public DeskCodesController(IDeskCodeService codes)
    {
        _codes = codes;
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<IReadOnlyList<DeskCodeMatch>>> Resolve(string code, CancellationToken cancellationToken)
        => Ok(await _codes.ResolveAsync(code, cancellationToken));
}
