using System.Globalization;
using System.Net;
using Eshop.Operations.Desktop.Api;
using Eshop.Operations.Desktop.Api.Payments;
using Eshop.Operations.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eshop.Operations.Desktop.Tests.ViewModels;

public sealed class PaymentsViewModelTests
{
    [Fact]
    public async Task LoadPaymentsCommandLoadsPayments()
    {
        PaymentDto payment = CreatePayment();
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>([payment])));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);

        Assert.Same(payment, Assert.Single(viewModel.Payments));
        Assert.True(viewModel.HasLoaded);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadPaymentsCommandMapsForbiddenResponse()
    {
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromException<IReadOnlyList<PaymentDto>>(
                    new ApiRequestException(HttpStatusCode.Forbidden, null))));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);

        Assert.Equal(
            "Your account does not have permission to access Payments.",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task FailedRefreshKeepsPreviouslyLoadedPayments()
    {
        PaymentDto payment = CreatePayment();
        var callCount = 0;
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => ++callCount == 1
                    ? Task.FromResult<IReadOnlyList<PaymentDto>>([payment])
                    : Task.FromException<IReadOnlyList<PaymentDto>>(
                        new HttpRequestException("Gateway unavailable."))));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);
        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);

        Assert.Same(payment, Assert.Single(viewModel.Payments));
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshPreservesSelectionWhenPaymentStillExists()
    {
        PaymentDto original = CreatePayment();
        PaymentDto refreshed = original with
        {
            Status = "Succeeded",
            ProcessedAtUtc = original.CreatedAtUtc.AddSeconds(2)
        };
        var callCount = 0;
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>(
                    ++callCount == 1 ? [original] : [refreshed])));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);
        viewModel.SelectedPayment = original;
        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);

        Assert.Same(refreshed, viewModel.SelectedPayment);
    }

    [Fact]
    public async Task SearchTextFiltersPayments()
    {
        PaymentDto alpha = CreatePayment() with { CustomerId = "customer-alpha" };
        PaymentDto beta = CreatePayment() with { CustomerId = "customer-beta" };
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>([alpha, beta])));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);
        viewModel.SearchText = "beta";

        PaymentDto visible = Assert.Single(
            viewModel.PaymentsView.Cast<PaymentDto>());
        Assert.Same(beta, visible);
    }

    [Fact]
    public async Task SelectedStatusFiltersPayments()
    {
        PaymentDto pending = CreatePayment() with { Status = "Pending" };
        PaymentDto failed = CreatePayment() with { Status = "Failed" };
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>([pending, failed])));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);
        viewModel.SelectedStatus = "Pending";

        PaymentDto visible = Assert.Single(
            viewModel.PaymentsView.Cast<PaymentDto>());
        Assert.Same(pending, visible);
    }

    [Fact]
    public async Task FilteringOutSelectedPaymentClearsSelection()
    {
        PaymentDto payment = CreatePayment();
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>([payment])));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);
        viewModel.SelectedPayment = payment;
        viewModel.SearchText = "does-not-match";

        Assert.Null(viewModel.SelectedPayment);
        Assert.True(viewModel.IsFilteredEmpty);
    }

    [Fact]
    public async Task ResetViewCommandClearsFilters()
    {
        PaymentDto succeeded = CreatePayment() with
        {
            CustomerId = "customer-succeeded",
            Status = "Succeeded"
        };
        PaymentDto failed = CreatePayment() with
        {
            CustomerId = "customer-failed",
            Status = "Failed"
        };
        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>([succeeded, failed])));

        await viewModel.LoadPaymentsCommand.ExecuteAsync(null);
        viewModel.SearchText = "does-not-match";
        viewModel.SelectedStatus = "Failed";
        viewModel.ResetViewCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("All statuses", viewModel.SelectedStatus);
        Assert.Equal(2, viewModel.PaymentsView.Cast<PaymentDto>().Count());
    }

    [Fact]
    public async Task FocusOrderAsyncFiltersPaymentsByOrderId()
    {
        Guid targetOrderId = Guid.NewGuid();
        PaymentDto target = CreatePayment() with { Id = Guid.NewGuid(), OrderId = targetOrderId };
        PaymentDto other = CreatePayment() with { Id = Guid.NewGuid(), OrderId = Guid.NewGuid() };

        PaymentsViewModel viewModel = CreateViewModel(
            new StubPaymentsApiClient(
                _ => Task.FromResult<IReadOnlyList<PaymentDto>>([target, other])));

        await viewModel.FocusOrderAsync(
            targetOrderId,
            TestContext.Current.CancellationToken);

        PaymentDto visible = Assert.Single(viewModel.PaymentsView.Cast<PaymentDto>());
        Assert.Same(target, visible);
        Assert.Equal(targetOrderId.ToString("D"), viewModel.SearchText);
    }

    private static PaymentsViewModel CreateViewModel(
        IPaymentsApiClient apiClient) =>
        new(apiClient, NullLogger<PaymentsViewModel>.Instance);

    private static PaymentDto CreatePayment() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-123",
            1499.50m,
            "CZK",
            "test-success",
            "Pending",
            null,
            DateTimeOffset.Parse(
                "2026-08-29T10:00:00+00:00",
                CultureInfo.InvariantCulture),
            null);

    private sealed class StubPaymentsApiClient(
        Func<CancellationToken, Task<IReadOnlyList<PaymentDto>>> getPayments)
        : IPaymentsApiClient
    {
        public Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
            CancellationToken cancellationToken) =>
            getPayments(cancellationToken);
    }
}
