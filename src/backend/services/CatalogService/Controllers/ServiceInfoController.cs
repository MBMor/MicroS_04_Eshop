using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers;

[ApiController]
[Route("")]
public sealed class ServiceInfoController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Service = "CatalogService",
            Status = "Running"
        });
    }
}
