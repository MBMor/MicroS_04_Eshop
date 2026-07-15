using InventoryService.Domain;

namespace InventoryService.Application;

public enum InventoryMutationStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationFailed
}

public sealed record InventoryMutationResult(
    InventoryMutationStatus Status,
    InventoryItem? Item,
    string? Error)
{
    public static InventoryMutationResult Succeeded(
        InventoryItem item)
    {
        return new InventoryMutationResult(
            InventoryMutationStatus.Success,
            item,
            null);
    }

    public static InventoryMutationResult NotFound(
        string error)
    {
        return new InventoryMutationResult(
            InventoryMutationStatus.NotFound,
            null,
            error);
    }

    public static InventoryMutationResult Conflict(
        string error)
    {
        return new InventoryMutationResult(
            InventoryMutationStatus.Conflict,
            null,
            error);
    }

    public static InventoryMutationResult ValidationFailed(
        string error)
    {
        return new InventoryMutationResult(
            InventoryMutationStatus.ValidationFailed,
            null,
            error);
    }
}
