using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Application;
using OrdersService.Contracts;
using OrdersService.Domain;
using OrdersService.Identity;

namespace OrdersService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/orders")]
public sealed class OrdersController(
    OrderApplicationService orderService,
    IOrderOwnerProvider orderOwnerProvider) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Create an idempotent checkout order")]
    [EndpointDescription(
        "Idempotency-Key is required. The first committed request returns 201 Created. " +
        "An identical replay returns the same order with 200 OK and " +
        "Idempotent-Replayed: true; changed checkout data returns 409 Conflict.")]
    [Consumes("application/json")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        CreateOrderRequest request,
        [FromHeader(Name = OrderHeaders.IdempotencyKey)]
        [Required]
        [StringLength(128, MinimumLength = 1)]
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdempotencyKey(idempotencyKey))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid idempotency key.",
                "Idempotency-Key must contain 1 to 128 visible ASCII characters without whitespace.",
                "urn:eshop:problem:invalid-idempotency-key"));
        }

        string? customerId =
            orderOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Order owner could not be resolved.",
                "A valid authenticated subject claim is required."));
        }

        try
        {
            CreateOrderResult result =
                await orderService.CreateAsync(
                    customerId,
                    request.CustomerEmail,
                    request.PaymentMethod,
                    idempotencyKey,
                    cancellationToken);

            return result.Status switch
            {
                CreateOrderStatus.Success
                    when result.Order is not null
                    => CreateSuccessResponse(
                        result.Order,
                        result.IsReplay),

                CreateOrderStatus.EmptyBasket
                    => BadRequest(CreateProblem(
                        StatusCodes.Status400BadRequest,
                        "Checkout failed.",
                        result.Error)),

                CreateOrderStatus.MultipleCurrencies
                    => BadRequest(CreateProblem(
                        StatusCodes.Status400BadRequest,
                        "Checkout failed.",
                        result.Error)),

                CreateOrderStatus.IdempotencyConflict
                    => Conflict(CreateProblem(
                        StatusCodes.Status409Conflict,
                        "Idempotency key was reused.",
                        result.Error,
                        "urn:eshop:problem:idempotency-key-reused")),

                _ => throw new InvalidOperationException(
                    "Unexpected create order result.")
            };
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Basket Service is unavailable.",
                    "The basket could not be loaded for checkout."));
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Basket Service timed out.",
                    "The basket could not be loaded for checkout."));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponse>> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        string? customerId =
            orderOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Order owner could not be resolved.",
                "Authentication or a development customer header is required."));
        }

        Order? order = await orderService.GetAsync(
            customerId,
            id,
            cancellationToken);

        if (order is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Order was not found.",
                "The order does not exist or belongs to another customer."));
        }

        return Ok(OrderResponse.FromOrder(order));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryResponse>>> GetOrders(
        CancellationToken cancellationToken)
    {
        string? customerId =
            orderOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Order owner could not be resolved.",
                "Authentication or a development customer header is required."));
        }

        List<Order> orders = await orderService.ListAsync(
            customerId,
            cancellationToken);

        OrderSummaryResponse[] response = orders
            .Select(OrderSummaryResponse.FromOrder)
            .ToArray();

        return Ok(response);
    }

    private static ProblemDetails CreateProblem(
        int status,
        string title,
        string? detail,
        string? type = null)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = type
        };
    }

    private ActionResult<OrderResponse> CreateSuccessResponse(
        Order order,
        bool isReplay)
    {
        object routeValues = new
        {
            version = RouteData.Values["version"],
            id = order.Id
        };

        if (!isReplay)
        {
            return CreatedAtAction(
                nameof(GetOrderById),
                routeValues,
                OrderResponse.FromOrder(order));
        }

        string? location = Url.Action(
            nameof(GetOrderById),
            values: routeValues);

        if (location is not null)
        {
            Response.Headers.Location = location;
        }

        Response.Headers[OrderHeaders.IdempotentReplayed] =
            bool.TrueString.ToLowerInvariant();

        return Ok(OrderResponse.FromOrder(order));
    }

    private static bool IsValidIdempotencyKey(
        string idempotencyKey)
    {
        return idempotencyKey.Length is >= 1 and <= 128
            && idempotencyKey.All(character =>
                character is >= '!' and <= '~');
    }
}
