using Microsoft.AspNetCore.Mvc;

namespace OrdersService.Controllers;

[ApiController]
[Route("")]
public sealed class ServiceInfoController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Service = "OrdersService",
            Status = "Running"
        });
    }
}
