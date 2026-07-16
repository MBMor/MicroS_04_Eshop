using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NotificationsService.Application;
using NotificationsService.Contracts;
using NotificationsService.Domain;
using NotificationsService.Identity;

namespace NotificationsService.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(
    NotificationApplicationService notificationService,
    INotificationOwnerProvider notificationOwnerProvider)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<NotificationResponse>>> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] Guid? orderId = null,
        [FromQuery, Range(1, 100)] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (orderId.HasValue
            && orderId.Value == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(orderId),
                "OrderId must not be an empty GUID.");

            return ValidationProblem(ModelState);
        }

        string? customerId =
            notificationOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        IReadOnlyList<Notification> notifications =
            await notificationService.ListAsync(
                customerId,
                unreadOnly,
                orderId,
                limit,
                cancellationToken);

        NotificationResponse[] response = notifications
            .Select(NotificationResponse.FromNotification)
            .ToArray();

        return Ok(response);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<
        UnreadNotificationCountResponse>>
        GetUnreadNotificationCount(
            CancellationToken cancellationToken)
    {
        string? customerId =
            notificationOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        int count = await notificationService.CountUnreadAsync(
            customerId,
            cancellationToken);

        return Ok(
            new UnreadNotificationCountResponse(count));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationResponse>>
        GetNotificationById(
            Guid id,
            CancellationToken cancellationToken)
    {
        string? customerId =
            notificationOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        Notification? notification =
            await notificationService.GetByIdAsync(
                customerId,
                id,
                cancellationToken);

        if (notification is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Notification was not found.",
                "The notification does not exist or belongs to another customer."));
        }

        return Ok(
            NotificationResponse.FromNotification(notification));
    }

    private static ProblemDetails CreateUnauthorizedProblem()
    {
        return CreateProblem(
            StatusCodes.Status401Unauthorized,
            "Notification owner could not be resolved.",
            "Authentication or a development customer header is required.");
    }

    private static ProblemDetails CreateProblem(
        int status,
        string title,
        string? detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
