namespace Eshop.Operations.Desktop.Models;

public enum CatalogLoadErrorKind
{
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    RateLimited,
    ServerFailure,
    Connectivity,
    Timeout,
    HttpFailure,
    Unexpected
}

public sealed record CatalogLoadError(
    CatalogLoadErrorKind Kind,
    string Message,
    int? StatusCode = null,
    string? TraceId = null,
    string? RequestId = null)
{
    public string? DiagnosticReference =>
        !string.IsNullOrWhiteSpace(TraceId)
            ? TraceId
            : RequestId;

    public bool HasDiagnosticReference =>
        !string.IsNullOrWhiteSpace(DiagnosticReference);
}
