namespace Eshop.Operations.Desktop.Api.Payments;

public interface IPaymentsApiClient
{
    Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
        CancellationToken cancellationToken);
}
