using System.ComponentModel;

namespace Eshop.Operations.Desktop.Models;

public sealed record CatalogSortOption(
    string DisplayName,
    string PropertyName,
    ListSortDirection Direction);
