namespace ErrorHandling.Shared.Exceptions;

public sealed class ResourceConflictException(
    string message,
    string errorCode = "resource_conflict",
    Exception? innerException = null)
    : EshopException(
        message,
        errorCode,
        innerException);
