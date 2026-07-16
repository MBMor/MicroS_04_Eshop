namespace ErrorHandling.Shared.Exceptions;

public sealed class ResourceNotFoundException(
    string message,
    string errorCode = "resource_not_found",
    Exception? innerException = null)
    : EshopException(
        message,
        errorCode,
        innerException);
