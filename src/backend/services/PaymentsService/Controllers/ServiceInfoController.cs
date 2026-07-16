using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace PaymentsService.Controllers;

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
            Service = "PaymentsService",
            Status = "Running"
        });
    }
}
