using Asp.Versioning;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrdersService.Controllers;

[ApiVersionNeutral]
[ApiController]
[Authorize(Policy = EshopPolicies.CustomerOnly)]
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
