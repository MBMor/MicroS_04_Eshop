using Asp.Versioning;
using BasketService.Application;
using BasketService.Contracts;
using BasketService.Domain;
using BasketService.Identity;
using BasketService.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BasketService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/basket")]
public sealed class BasketController(
    BasketApplicationService basketService,
    IBasketOwnerProvider basketOwnerProvider,
    IOptions<BasketOptions> basketOptions) : ControllerBase
{
    private readonly BasketOptions _basketOptions = basketOptions.Value;

    [HttpGet]
    [ProducesResponseType<BasketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BasketResponse>> GetBasket(
        CancellationToken cancellationToken)
    {
        string? customerId = basketOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Basket owner could not be resolved.",
                "A valid authenticated subject claim is required."));
        }

        ShoppingBasket basket = await basketService.GetAsync(
            customerId,
            cancellationToken);

        return Ok(ToResponse(basket));
    }

    [HttpPost("items")]
    [Consumes("application/json")]
    [ProducesResponseType<BasketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BasketResponse>> AddItem(
        AddBasketItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(request.ProductId),
                "ProductId must not be empty.");

            return ValidationProblem(ModelState);
        }

        string? customerId = basketOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Basket owner could not be resolved.",
                "Authentication or a development customer header is required."));
        }

        try
        {
            BasketMutationResult result = await basketService.AddItemAsync(
                customerId,
                request.ProductId,
                request.Quantity,
                cancellationToken);

            return MapMutationResult(result);
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Catalog Service is unavailable.",
                    "The product could not be validated."));
        }
    }

    [HttpPut("items/{productId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<BasketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BasketResponse>> UpdateItem(
        Guid productId,
        UpdateBasketItemRequest request,
        CancellationToken cancellationToken)
    {
        string? customerId = basketOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Basket owner could not be resolved.",
                "Authentication or a development customer header is required."));
        }

        BasketMutationResult result =
            await basketService.UpdateQuantityAsync(
                customerId,
                productId,
                request.Quantity,
                cancellationToken);

        return MapMutationResult(result);
    }

    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveItem(
        Guid productId,
        CancellationToken cancellationToken)
    {
        string? customerId = basketOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Basket owner could not be resolved.",
                "Authentication or a development customer header is required."));
        }

        bool removed = await basketService.RemoveItemAsync(
            customerId,
            productId,
            cancellationToken);

        return removed
            ? NoContent()
            : NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Basket item was not found.",
                "The requested product is not present in the basket."));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ClearBasket(
        CancellationToken cancellationToken)
    {
        string? customerId = basketOwnerProvider.GetCustomerId(HttpContext);

        if (customerId is null)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Basket owner could not be resolved.",
                "Authentication or a development customer header is required."));
        }

        await basketService.ClearAsync(
            customerId,
            cancellationToken);

        return NoContent();
    }

    private ActionResult<BasketResponse> MapMutationResult(
        BasketMutationResult result)
    {
        return result.Status switch
        {
            BasketMutationStatus.Success when result.Basket is not null
                => Ok(ToResponse(result.Basket)),

            BasketMutationStatus.NotFound
                => NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Basket operation failed.",
                    result.Error)),

            BasketMutationStatus.ValidationFailed
                => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Basket validation failed.",
                    result.Error)),

            _ => throw new InvalidOperationException(
                "Unexpected basket mutation result.")
        };
    }

    private BasketResponse ToResponse(ShoppingBasket basket)
    {
        return BasketResponse.FromBasket(
            basket,
            _basketOptions.ExpirationMinutes);
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
