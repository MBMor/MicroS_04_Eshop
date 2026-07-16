namespace ErrorHandling.Shared.Exceptions;

public abstract class EshopException(
    string message,
    string errorCode,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}
