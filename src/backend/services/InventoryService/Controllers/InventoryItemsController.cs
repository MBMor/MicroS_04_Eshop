using Asp.Versioning;
using InventoryService.Application;
using InventoryService.Contracts;
using InventoryService.Domain;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/inventory-items")]
public sealed class InventoryItemsController(
    InventoryApplicationService inventoryService)
    : ControllerBase
{
    [HttpGet]
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

    [HttpPost("{id:guid}/stock-adjustments")]
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

            return ValidationProblem(ModelState);
        }

        InventoryMutationResult result =
            await inventoryService.AdjustStockAsync(
                id,
                request.QuantityDelta,
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

    private ActionResult<InventoryItemResponse> MapFailure(
        InventoryMutationResult result)
    {
        return result.Status switch
        {
            InventoryMutationStatus.NotFound
                => NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Inventory item was not found.",
                    result.Error)),

            InventoryMutationStatus.Conflict
                => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Inventory conflict.",
                    result.Error)),

            InventoryMutationStatus.ValidationFailed
                => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Inventory validation failed.",
                    result.Error)),

            _ => throw new InvalidOperationException(
                $"Unexpected inventory mutation status '{result.Status}'.")
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
