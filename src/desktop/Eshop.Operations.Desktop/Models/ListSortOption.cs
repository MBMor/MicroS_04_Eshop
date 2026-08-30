using System.ComponentModel;

namespace Eshop.Operations.Desktop.Models;

public sealed record ListSortOption(
    string DisplayName,
    string PropertyName,
    ListSortDirection Direction);
