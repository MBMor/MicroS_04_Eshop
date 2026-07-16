using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace NotificationsService.Controllers;

[ApiVersionNeutral]
[ApiController]
[Route("")]
public sealed class ServiceInfoController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Service = "NotificationsService",
            Status = "Running"
        });
    }
}
