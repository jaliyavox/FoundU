using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Support.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Ask before opening a ticket. The assistant answers from FoundU's help guide, using the
/// caller's own records; when it cannot help it returns a ticket draft, which the caller sends
/// through <c>POST /api/support/tickets</c>. Nothing here writes.
/// </summary>
[ApiController]
[Route("api/support/assistant")]
[Authorize]
public class SupportAssistantController : ControllerBase
{
    private readonly ISupportAssistantService _assistant;

    public SupportAssistantController(ISupportAssistantService assistant)
    {
        _assistant = assistant;
    }

    [HttpPost]
    public async Task<ActionResult<SupportAssistantResponse>> Ask(
        [FromBody] SupportAssistantRequest request,
        CancellationToken cancellationToken)
        => Ok(await _assistant.AskAsync(request, User.GetUserId(), cancellationToken));
}
