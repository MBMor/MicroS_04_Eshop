using Eshop.Operations.Desktop.Api.Inventory;
using Eshop.Operations.Desktop.Models;

namespace Eshop.Operations.Desktop.Services;

public interface IInventoryStockAdjustmentDialogService
{
    InventoryStockAdjustmentDraft? ShowConfirmation(InventoryItemDto item);
}
