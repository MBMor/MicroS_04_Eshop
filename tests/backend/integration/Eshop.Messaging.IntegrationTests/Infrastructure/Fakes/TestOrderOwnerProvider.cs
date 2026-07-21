using OrdersService.Identity;

namespace Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;

public sealed class TestOrderOwnerProvider : IOrderOwnerProvider
{
    public const string CustomerIdHeaderName =
        "X-Customer-Id";

    private const int MaxCustomerIdLength = 128;

    public string? GetCustomerId(
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!httpContext.Request.Headers.TryGetValue(
                CustomerIdHeaderName,
                out Microsoft.Extensions.Primitives.StringValues
                    headerValues))
        {
            return null;
        }

        string customerId =
            headerValues.ToString().Trim();

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        return customerId.Length <= MaxCustomerIdLength
            ? customerId
            : null;
    }
}
