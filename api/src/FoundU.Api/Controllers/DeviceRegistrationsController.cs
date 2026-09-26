using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>Authenticated, owner-scoped registration of device delivery tokens.</summary>
[ApiController]
[Route("api/device-registrations")]
[Authorize]
public class DeviceRegistrationsController : ControllerBase
{
    private readonly IDeviceRegistrationService _registrations;

    public DeviceRegistrationsController(IDeviceRegistrationService registrations) => _registrations = registrations;

    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        await _registrations.RegisterAsync(User.GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister(UnregisterDeviceRequest request, CancellationToken cancellationToken)
    {
        await _registrations.UnregisterAsync(User.GetUserId(), request, cancellationToken);
        return NoContent();
    }
}
