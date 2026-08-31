using System.Diagnostics;
using Asp.Versioning;
using Eshop.Observability;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Application;
using OrdersService.Contracts;
using OrdersService.Domain;

namespace OrdersService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize(Policy = EshopPolicies.SupportOrAdmin)]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/operations/orders")]
public sealed class OperationalOrdersController(
    OrderApplicationService orderService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<OperationalOrderPageResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationalOrderPageResponse>>
        GetOrders(
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
    {
        if (offset < 0)
        {
            ModelState.AddModelError(
                nameof(offset),
                "Offset must not be negative.");
        }

        if (limit is < 1 or > 100)
        {
            ModelState.AddModelError(
                nameof(limit),
                "Limit must be between 1 and 100.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OperationalOrderPage page =
            await orderService.ListOperationalAsync(
                offset,
                limit,
                cancellationToken);

        OperationalOrderSummaryResponse[] items =
            page.Items
                .Select(OperationalOrderSummaryResponse.FromOrder)
                .ToArray();

        return Ok(
            new OperationalOrderPageResponse(
                items,
                page.Offset,
                page.Limit,
                page.HasMore));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OperationalOrderResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalOrderResponse>>
        GetOrder(
            Guid id,
            CancellationToken cancellationToken)
    {
        Activity.Current?.SetTag(
            BusinessTelemetryTagNames.OrderId,
            id.ToString("D"));

        Order? order =
            await orderService.GetOperationalAsync(
                id,
                cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        return Ok(
            OperationalOrderResponse.FromOrder(order));
    }
}
