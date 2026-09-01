namespace Eshop.Operations.Desktop.Api.OperationalHealth;

public interface IOperationalHealthApiClient
{
    Task<OperationalHealthResponseDto>
        GetOperationalHealthAsync(
            CancellationToken cancellationToken);
}
