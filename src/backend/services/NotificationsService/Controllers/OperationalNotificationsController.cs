using Asp.Versioning;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationsService.Application;
using NotificationsService.Contracts;
using NotificationsService.Domain;

namespace NotificationsService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize(Policy = EshopPolicies.SupportOrAdmin)]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/operations/notifications")]
public sealed class OperationalNotificationsController(
    NotificationApplicationService applicationService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<OperationalNotificationPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationalNotificationPageResponse>> List(
        [FromQuery] Guid? orderId = null,
        [FromQuery] string? customerId = null,
        [FromQuery] Guid? correlationId = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
        {
            ModelState.AddModelError(nameof(offset), "Offset must not be negative.");
        }

        if (limit is < 1 or > 100)
        {
            ModelState.AddModelError(nameof(limit), "Limit must be between 1 and 100.");
        }

        if (orderId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(orderId), "Order id must not be empty.");
        }

        if (correlationId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(correlationId), "Correlation id must not be empty.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        OperationalNotificationPage page =
            await applicationService.ListOperationalAsync(
                orderId,
                customerId,
                correlationId,
                offset,
                limit,
                cancellationToken);

        return Ok(new OperationalNotificationPageResponse(
            page.Items
                .Select(OperationalNotificationResponse.FromNotification)
                .ToArray(),
            page.Offset,
            page.Limit,
            page.HasMore));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OperationalNotificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalNotificationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Notification? notification =
            await applicationService.GetOperationalByIdAsync(
                id,
                cancellationToken);

        if (notification is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Notification was not found."
            });
        }

        return Ok(OperationalNotificationResponse.FromNotification(notification));
    }
}
