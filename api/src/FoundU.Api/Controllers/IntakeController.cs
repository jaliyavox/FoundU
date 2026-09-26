using FoundU.Api.Extensions;
using FoundU.Application.Auth;
using FoundU.Application.Intake;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// "Ask FoundU" - a student says what they lost, the agent asks for what it still needs, and
/// the API searches what has been found. Read-only: it suggests, and the student acts through
/// the ordinary report, recognition and claim endpoints. Staff still verify ownership.
/// </summary>
[ApiController]
[Route("api/intake")]
[Authorize(Policy = PolicyNames.Student)]
public class IntakeController : ControllerBase
{
    private readonly IIntakeService _intake;

    public IntakeController(IIntakeService intake)
    {
        _intake = intake;
    }

    [HttpPost]
    public async Task<ActionResult<IntakeResponse>> Ask(
        [FromBody] IntakeRequest request,
        CancellationToken cancellationToken)
        => Ok(await _intake.AskAsync(request, User.GetUserId(), cancellationToken));
}
