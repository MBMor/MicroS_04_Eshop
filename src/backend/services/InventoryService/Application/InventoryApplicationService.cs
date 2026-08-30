using InventoryService.Data;
using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public async Task<InventoryStockAdjustmentExecutionResult>
        AdjustStockAsync(
            InventoryStockAdjustmentCommand command,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        string normalizedReason = command.Reason.Trim();

        InventoryStockAdjustmentOperation? existingOperation =
            await dbContext.InventoryStockAdjustmentOperations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    operation =>
                        operation.IdempotencyKey
                        == command.IdempotencyKey,
                    cancellationToken);

        if (existingOperation is not null)
        {
            return ResolveExistingStockAdjustment(
                existingOperation,
                command,
                normalizedReason);
        }

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        InventoryStockAdjustmentOperation operation =
            InventoryStockAdjustmentOperation.Begin(
                command.IdempotencyKey,
                command.InventoryItemId,
                command.QuantityDelta,
                command.ExpectedVersion,
                normalizedReason,
                command.ActorSubject,
                command.ActorUsername,
                command.TraceId,
                timeProvider.GetUtcNow());

        dbContext.InventoryStockAdjustmentOperations.Add(operation);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            InventoryStockAdjustmentOperation racedOperation =
                await dbContext.InventoryStockAdjustmentOperations
                    .AsNoTracking()
                    .SingleAsync(
                        candidate =>
                            candidate.IdempotencyKey
                            == command.IdempotencyKey,
                        cancellationToken);

            return ResolveExistingStockAdjustment(
                racedOperation,
                command,
                normalizedReason);
        }

        InventoryItem? inventoryItem =
            await dbContext.InventoryItems
                .FirstOrDefaultAsync(
                    item => item.Id == command.InventoryItemId,
                    cancellationToken);

        if (inventoryItem is null)
        {
            return await CompleteStockAdjustmentFailureAsync(
                operation,
                InventoryStockAdjustmentOutcome.NotFound,
                "Inventory item was not found.",
                transaction,
                cancellationToken);
        }

        if (inventoryItem.Version != command.ExpectedVersion)
        {
            return await CompleteStockAdjustmentFailureAsync(
                operation,
                InventoryStockAdjustmentOutcome.Conflict,
                "Inventory item has changed since it was loaded. " +
                "Refresh the item and try again.",
                transaction,
                cancellationToken);
        }

        int onHandBefore = inventoryItem.OnHandQuantity;
        int reservedBefore = inventoryItem.ReservedQuantity;
        int availableBefore = inventoryItem.AvailableQuantity;

        try
        {
            inventoryItem.AdjustOnHandQuantity(
                command.QuantityDelta,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return await CompleteStockAdjustmentFailureAsync(
                operation,
                InventoryStockAdjustmentOutcome.ValidationFailed,
                exception.Message,
                transaction,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return await CompleteStockAdjustmentFailureAsync(
                operation,
                InventoryStockAdjustmentOutcome.ValidationFailed,
                exception.Message,
                transaction,
                cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(inventoryItem).State = EntityState.Detached;

            return await CompleteStockAdjustmentFailureAsync(
                operation,
                InventoryStockAdjustmentOutcome.Conflict,
                "Inventory item changed while the stock adjustment " +
                "was being applied. Refresh the item and try again.",
                transaction,
                cancellationToken);
        }

        operation.CompleteSuccess(
            inventoryItem,
            onHandBefore,
            reservedBefore,
            availableBefore);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        dbContext.ChangeTracker.Clear();

        InventoryStockAdjustmentOperation committedOperation =
            await dbContext.InventoryStockAdjustmentOperations
                .AsNoTracking()
                .SingleAsync(
                    candidate => candidate.Id == operation.Id,
                    cancellationToken);

        return InventoryStockAdjustmentExecutionResult.FromOperation(
            committedOperation,
            isReplay: false);
    }

    private static InventoryStockAdjustmentExecutionResult
        ResolveExistingStockAdjustment(
            InventoryStockAdjustmentOperation operation,
            InventoryStockAdjustmentCommand command,
            string normalizedReason)
    {
        if (!operation.MatchesRequest(
                command.InventoryItemId,
                command.QuantityDelta,
                command.ExpectedVersion,
                normalizedReason,
                command.ActorSubject))
        {
            return InventoryStockAdjustmentExecutionResult.Conflict(
                "The supplied Idempotency-Key has already been used " +
                "for a different stock adjustment request.");
        }

        if (operation.Outcome
            == InventoryStockAdjustmentOutcome.Pending)
        {
            return InventoryStockAdjustmentExecutionResult.Conflict(
                "The stock adjustment operation is still being processed. " +
                "Retry with the same Idempotency-Key.");
        }

        return InventoryStockAdjustmentExecutionResult.FromOperation(
            operation,
            isReplay: true);
    }

    private async Task<InventoryStockAdjustmentExecutionResult>
        CompleteStockAdjustmentFailureAsync(
            InventoryStockAdjustmentOperation operation,
            InventoryStockAdjustmentOutcome outcome,
            string error,
            IDbContextTransaction transaction,
            CancellationToken cancellationToken)
    {
        operation.CompleteFailure(outcome, error);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return InventoryStockAdjustmentExecutionResult.FromOperation(
            operation,
            isReplay: false);
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
