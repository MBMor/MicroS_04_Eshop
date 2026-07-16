using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

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
            Service = "InventoryService",
            Status = "Running"
        });
    }
}
