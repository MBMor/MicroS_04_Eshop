using Microsoft.AspNetCore.Mvc;
using OrdersService.Application;
using OrdersService.Contracts;
using OrdersService.Domain;
using OrdersService.Identity;

namespace OrdersService.Controllers;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController(
    OrderApplicationService orderService,
    IOrderOwnerProvider orderOwnerProvider) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        CreateOrderRequest request,
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

        try
        {
            CreateOrderResult result =
                await orderService.CreateAsync(
                    customerId,
                    request.CustomerEmail,
                    request.PaymentMethod,
                    cancellationToken);

            return result.Status switch
            {
                CreateOrderStatus.Success
                    when result.Order is not null
                    => CreatedAtAction(
                        nameof(GetOrderById),
                        new { id = result.Order.Id },
                        OrderResponse.FromOrder(result.Order)),

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
