using FoundU.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = AppInfo.Describe(),
        timeUtc = DateTime.UtcNow
    });
}
