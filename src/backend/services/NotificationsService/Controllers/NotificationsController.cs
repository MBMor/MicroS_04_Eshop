using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using NotificationsService.Application;
using NotificationsService.Contracts;
using NotificationsService.Domain;
using NotificationsService.Identity;

namespace NotificationsService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController(
    NotificationApplicationService notificationService,
    INotificationOwnerProvider notificationOwnerProvider)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType<UnreadNotificationCountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType<NotificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
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
            "A valid authenticated subject claim is required.");
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
