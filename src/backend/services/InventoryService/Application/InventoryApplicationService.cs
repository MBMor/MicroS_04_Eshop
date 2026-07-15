using InventoryService.Data;
using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InventoryService.Application;

public sealed class InventoryApplicationService(
    InventoryDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InventoryItem>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        IQueryable<InventoryItem> query =
            dbContext.InventoryItems.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .OrderBy(item => item.Sku)
            .ToListAsync(cancellationToken);
    }

    public Task<InventoryItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return dbContext.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);
    }

    public Task<InventoryItem?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return dbContext.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ProductId == productId,
                cancellationToken);
    }

    public async Task<InventoryMutationResult> CreateAsync(
        Guid productId,
        string sku,
        int initialOnHandQuantity,
        bool isActive,
        CancellationToken cancellationToken)
    {
        string normalizedSku = NormalizeSku(sku);

        bool productAlreadyExists =
            await dbContext.InventoryItems.AnyAsync(
                item => item.ProductId == productId,
                cancellationToken);

        if (productAlreadyExists)
        {
            return InventoryMutationResult.Conflict(
                $"An inventory item for product '{productId}' already exists.");
        }

        bool skuAlreadyExists =
            await dbContext.InventoryItems.AnyAsync(
                item => item.Sku == normalizedSku,
                cancellationToken);

        if (skuAlreadyExists)
        {
            return InventoryMutationResult.Conflict(
                $"Inventory SKU '{normalizedSku}' already exists.");
        }

        InventoryItem inventoryItem;

        try
        {
            inventoryItem = InventoryItem.Create(
                Guid.NewGuid(),
                productId,
                normalizedSku,
                initialOnHandQuantity,
                isActive,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return InventoryMutationResult.ValidationFailed(
                exception.Message);
        }

        dbContext.InventoryItems.Add(inventoryItem);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            return InventoryMutationResult.Conflict(
                "An inventory item with the same product id or SKU already exists.");
        }

        return InventoryMutationResult.Succeeded(
            inventoryItem);
    }

    public async Task<InventoryMutationResult> UpdateAsync(
        Guid id,
        string sku,
        int onHandQuantity,
        bool isActive,
        CancellationToken cancellationToken)
    {
        InventoryItem? inventoryItem =
            await dbContext.InventoryItems.FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (inventoryItem is null)
        {
            return InventoryMutationResult.NotFound(
                "Inventory item was not found.");
        }

        string normalizedSku = NormalizeSku(sku);

        bool skuAlreadyExists =
            await dbContext.InventoryItems.AnyAsync(
                item =>
                    item.Id != id
                    && item.Sku == normalizedSku,
                cancellationToken);

        if (skuAlreadyExists)
        {
            return InventoryMutationResult.Conflict(
                $"Inventory SKU '{normalizedSku}' already exists.");
        }

        try
        {
            inventoryItem.Update(
                normalizedSku,
                onHandQuantity,
                isActive,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return InventoryMutationResult.ValidationFailed(
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return InventoryMutationResult.ValidationFailed(
                exception.Message);
        }

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            return InventoryMutationResult.Conflict(
                $"Inventory SKU '{normalizedSku}' already exists.");
        }

        return InventoryMutationResult.Succeeded(
            inventoryItem);
    }

    public async Task<InventoryMutationResult> AdjustStockAsync(
        Guid id,
        int quantityDelta,
        CancellationToken cancellationToken)
    {
        InventoryItem? inventoryItem =
            await dbContext.InventoryItems.FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (inventoryItem is null)
        {
            return InventoryMutationResult.NotFound(
                "Inventory item was not found.");
        }

        try
        {
            inventoryItem.AdjustOnHandQuantity(
                quantityDelta,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return InventoryMutationResult.ValidationFailed(
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return InventoryMutationResult.ValidationFailed(
                exception.Message);
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return InventoryMutationResult.Succeeded(
            inventoryItem);
    }

    private static string NormalizeSku(string sku)
    {
        return sku.Trim().ToUpperInvariant();
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }
}
