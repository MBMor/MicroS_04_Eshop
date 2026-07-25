namespace Eshop.ErrorHandling.Exceptions;

public sealed class RequestValidationException(
    string message,
    string errorCode = "validation_failed",
    Exception? innerException = null)
    : EshopException(
        message,
        errorCode,
        innerException);
