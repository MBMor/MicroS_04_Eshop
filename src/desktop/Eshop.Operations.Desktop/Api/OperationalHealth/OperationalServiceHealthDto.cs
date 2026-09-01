namespace Eshop.Operations.Desktop.Api.OperationalHealth;

public sealed record OperationalServiceHealthDto(
    string Service,
    string Status,
    long DurationMilliseconds,
    string? FailureKind,
    int? HttpStatusCode,
    IReadOnlyList<string> FailedDependencies)
{
    public string FailedDependenciesText =>
        FailedDependencies.Count == 0
            ? "—"
            : string.Join(
                ", ",
                FailedDependencies);
}
