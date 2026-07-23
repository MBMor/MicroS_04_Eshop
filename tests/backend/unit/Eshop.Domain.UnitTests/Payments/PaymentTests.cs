using PaymentsService.Domain;
using Xunit;

namespace Eshop.Domain.UnitTests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            year: 2026,
            month: 7,
            day: 23,
            hour: 8,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero);

    [Fact]
    public void CreatePending_ValidData_NormalizesValues()
    {
        Guid paymentId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();

        Payment payment = Payment.CreatePending(
            paymentId,
            orderId,
            "  customer-1  ",
            amount: 250m,
            currency: "  czk  ",
            paymentMethod: "  TEST-SUCCESS  ",
            CreatedAtUtc);

        Assert.Equal(paymentId, payment.Id);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal("customer-1", payment.CustomerId);
        Assert.Equal(250m, payment.Amount);
        Assert.Equal("CZK", payment.Currency);
        Assert.Equal("test-success", payment.PaymentMethod);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.FailureReason);
        Assert.Equal(CreatedAtUtc, payment.CreatedAtUtc);
        Assert.Null(payment.ProcessedAtUtc);
    }

    [Fact]
    public void CreatePending_NonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Payment.CreatePending(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "customer-1",
                amount: 0m,
                currency: "CZK",
                paymentMethod: "test-success",
                CreatedAtUtc));
    }

    [Fact]
    public void Authorize_PendingPayment_MarksPaymentAuthorized()
    {
        Payment payment = CreatePayment();

        DateTimeOffset processedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        payment.Authorize(processedAtUtc);

        Assert.Equal(
            PaymentStatus.Authorized,
            payment.Status);

        Assert.Null(payment.FailureReason);

        Assert.Equal(
            processedAtUtc,
            payment.ProcessedAtUtc);
    }

    [Fact]
    public void Fail_PendingPayment_NormalizesFailureReason()
    {
        Payment payment = CreatePayment();

        DateTimeOffset processedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        payment.Fail(
            "  Simulated payment failure.  ",
            processedAtUtc);

        Assert.Equal(
            PaymentStatus.Failed,
            payment.Status);

        Assert.Equal(
            "Simulated payment failure.",
            payment.FailureReason);

        Assert.Equal(
            processedAtUtc,
            payment.ProcessedAtUtc);
    }

    [Fact]
    public void Fail_BlankReason_ThrowsWithoutMutation()
    {
        Payment payment = CreatePayment();

        Assert.Throws<ArgumentException>(
            () => payment.Fail(
                "   ",
                CreatedAtUtc.AddMinutes(1)));

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Null(payment.FailureReason);
        Assert.Null(payment.ProcessedAtUtc);
    }

    [Fact]
    public void Authorize_AlreadyAuthorizedPayment_Throws()
    {
        Payment payment = CreatePayment();

        payment.Authorize(
            CreatedAtUtc.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(
            () => payment.Authorize(
                CreatedAtUtc.AddMinutes(2)));

        Assert.Equal(
            PaymentStatus.Authorized,
            payment.Status);

        Assert.Equal(
            CreatedAtUtc.AddMinutes(1),
            payment.ProcessedAtUtc);
    }

    [Fact]
    public void Fail_AuthorizedPayment_ThrowsWithoutMutation()
    {
        Payment payment = CreatePayment();

        DateTimeOffset authorizedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        payment.Authorize(authorizedAtUtc);

        Assert.Throws<InvalidOperationException>(
            () => payment.Fail(
                "Payment failed too late.",
                CreatedAtUtc.AddMinutes(2)));

        Assert.Equal(
            PaymentStatus.Authorized,
            payment.Status);

        Assert.Null(payment.FailureReason);
        Assert.Equal(authorizedAtUtc, payment.ProcessedAtUtc);
    }

    private static Payment CreatePayment()
    {
        return Payment.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            amount: 250m,
            currency: "CZK",
            paymentMethod: "test-success",
            CreatedAtUtc);
    }
}
