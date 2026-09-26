using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Honor.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// "Help to find" - everything one person has done for someone else on the platform, the
/// codes they still need at a desk, and the honor points they have earned for it.
/// </summary>
[ApiController]
[Route("api/help-to-find")]
[Authorize(Policy = PolicyNames.Student)]
public class HelpToFindController : ControllerBase
{
    private readonly IHelpToFindService _helpToFind;

    public HelpToFindController(IHelpToFindService helpToFind)
    {
        _helpToFind = helpToFind;
    }

    [HttpGet]
    public async Task<ActionResult<HelpToFindDto>> Mine(CancellationToken cancellationToken)
        => Ok(await _helpToFind.GetAsync(User.GetUserId(), cancellationToken));
}
