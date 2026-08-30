using Asp.Versioning;
using InventoryService.Application;
using InventoryService.Contracts;
using InventoryService.Domain;
using Eshop.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/inventory-items")]
public sealed class InventoryItemsController(
    InventoryApplicationService inventoryService)
    : ControllerBase
{
    private const string IdempotencyKeyHeaderName =
        "Idempotency-Key";

    private const string IdempotentReplayHeaderName =
        "Idempotent-Replay";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InventoryItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<InventoryItemResponse>>>
        GetInventoryItems(
            [FromQuery] bool includeInactive = false,
            CancellationToken cancellationToken = default)
    {
        IReadOnlyList<InventoryItem> items =
            await inventoryService.ListAsync(
                includeInactive,
                cancellationToken);

        InventoryItemResponse[] response = items
            .Select(InventoryItemResponse.FromInventoryItem)
            .ToArray();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InventoryItemResponse>>
        GetInventoryItemById(
            Guid id,
            CancellationToken cancellationToken)
    {
        InventoryItem? item =
            await inventoryService.GetByIdAsync(
                id,
                cancellationToken);

        if (item is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Inventory item was not found.",
                $"Inventory item '{id}' does not exist."));
        }

        return Ok(
            InventoryItemResponse.FromInventoryItem(item));
    }

    [HttpGet("by-product/{productId:guid}")]
    [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InventoryItemResponse>>
        GetInventoryItemByProductId(
            Guid productId,
            CancellationToken cancellationToken)
    {
        InventoryItem? item =
            await inventoryService.GetByProductIdAsync(
                productId,
                cancellationToken);

        if (item is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Inventory item was not found.",
                $"No inventory item exists for product '{productId}'."));
        }

        return Ok(
            InventoryItemResponse.FromInventoryItem(item));
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InventoryItemResponse>>
        CreateInventoryItem(
            CreateInventoryItemRequest request,
            CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(request.ProductId),
                "ProductId must not be empty.");

            return ValidationProblem(ModelState);
        }

        InventoryMutationResult result =
            await inventoryService.CreateAsync(
                request.ProductId,
                request.Sku,
                request.InitialOnHandQuantity,
                request.IsActive,
                cancellationToken);

        if (result.Status == InventoryMutationStatus.Success
            && result.Item is not null)
        {
            InventoryItemResponse response =
                InventoryItemResponse.FromInventoryItem(
                    result.Item);

            return CreatedAtAction(
                nameof(GetInventoryItemById),
                new
                {
                    version = RouteData.Values["version"],
                    id = result.Item.Id
                },
                response);
        }

        return MapFailure(result);
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InventoryItemResponse>>
        UpdateInventoryItem(
            Guid id,
            UpdateInventoryItemRequest request,
            CancellationToken cancellationToken)
    {
        InventoryMutationResult result =
            await inventoryService.UpdateAsync(
                id,
                request.Sku,
                request.OnHandQuantity,
                request.IsActive,
                cancellationToken);

        if (result.Status == InventoryMutationStatus.Success
            && result.Item is not null)
        {
            return Ok(
                InventoryItemResponse.FromInventoryItem(
                    result.Item));
        }

        return MapFailure(result);
    }

    [Authorize(Policy = EshopPolicies.AdminOnly)]
    [HttpPost("{id:guid}/stock-adjustments")]
    [Consumes("application/json")]
    [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InventoryItemResponse>>
        AdjustInventoryStock(
            Guid id,
            AdjustInventoryStockRequest request,
            CancellationToken cancellationToken)
    {
        if (request.QuantityDelta == 0)
        {
            ModelState.AddModelError(
                nameof(request.QuantityDelta),
                "QuantityDelta must not be zero.");
        }

        if (request.ExpectedVersion == 0)
        {
            ModelState.AddModelError(
                nameof(request.ExpectedVersion),
                "ExpectedVersion must be greater than zero.");
        }

        string reason = request.Reason.Trim();

        if (reason.Length < 3)
        {
            ModelState.AddModelError(
                nameof(request.Reason),
                "Reason must contain at least 3 characters.");
        }
        else if (reason.Length > 500)
        {
            ModelState.AddModelError(
                nameof(request.Reason),
                "Reason must not exceed 500 characters.");
        }

        Guid idempotencyKey = Guid.Empty;

        if (!Request.Headers.TryGetValue(
                IdempotencyKeyHeaderName,
                out var idempotencyValues)
            || idempotencyValues.Count != 1
            || !Guid.TryParse(
                idempotencyValues[0],
                out idempotencyKey)
            || idempotencyKey == Guid.Empty)
        {
            ModelState.AddModelError(
                IdempotencyKeyHeaderName,
                "Idempotency-Key must contain exactly one non-empty GUID value.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        string? actorSubject = User.FindFirst(EshopClaimNames.Subject)
            ?.Value
            ?.Trim();

        if (string.IsNullOrWhiteSpace(actorSubject))
        {
            return Unauthorized(
                CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Authenticated user identity is incomplete.",
                    "The authenticated principal does not contain a subject claim."));
        }

        string? actorUsername =
            User.FindFirst(EshopClaimNames.PreferredUsername)
                ?.Value
                ?.Trim();

        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            actorUsername = actorSubject;
        }

        var command = new InventoryStockAdjustmentCommand(
            id,
            request.QuantityDelta,
            request.ExpectedVersion,
            reason,
            idempotencyKey,
            actorSubject,
            actorUsername,
            HttpContext.TraceIdentifier);

        InventoryStockAdjustmentExecutionResult result =
            await inventoryService.AdjustStockAsync(
                command,
                cancellationToken);

        if (result.IsReplay)
        {
            Response.Headers[IdempotentReplayHeaderName] = "true";
        }

        if (result.Status == InventoryMutationStatus.Success
            && result.Operation is not null)
        {
            return Ok(
                InventoryItemResponse.FromStockAdjustmentOperation(
                    result.Operation));
        }

        return MapFailure(result.Status, result.Error);
    }

    private ActionResult<InventoryItemResponse> MapFailure(
        InventoryMutationResult result)
    {
        return MapFailure(
            result.Status,
            result.Error);
    }

    private ActionResult<InventoryItemResponse> MapFailure(
        InventoryMutationStatus status,
        string? error)
    {
        return status switch
        {
            InventoryMutationStatus.NotFound
                => NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Inventory item was not found.",
                    error)),

            InventoryMutationStatus.Conflict
                => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Inventory conflict.",
                    error)),

            InventoryMutationStatus.ValidationFailed
                => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Inventory validation failed.",
                    error)),

            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblem(
                    StatusCodes.Status500InternalServerError,
                    "Inventory operation failed.",
                    error))
        };
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
