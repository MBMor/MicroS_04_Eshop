namespace ErrorHandling.Shared.Exceptions;

public sealed class RequestValidationException(
    string message,
    string errorCode = "validation_failed",
    Exception? innerException = null)
    : EshopException(
        message,
        errorCode,
        innerException);
