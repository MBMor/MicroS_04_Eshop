using PaymentsService.Application;
using Xunit;

namespace Eshop.Domain.UnitTests.Payments;

public sealed class FakePaymentProcessorTests
{
    private readonly FakePaymentProcessor _processor =
        new();

    [Fact]
    public void TryProcessSuccessMethodReturnsAuthorizedDecision()
    {
        bool recognized = _processor.TryProcess(
            "  TEST-SUCCESS  ",
            out FakePaymentDecision? decision);

        Assert.True(recognized);
        Assert.NotNull(decision);
        Assert.Equal("test-success", decision.PaymentMethod);
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.FailureReason);
    }

    [Fact]
    public void TryProcessFailureMethodReturnsFailedDecision()
    {
        bool recognized = _processor.TryProcess(
            "test-fail",
            out FakePaymentDecision? decision);

        Assert.True(recognized);
        Assert.NotNull(decision);
        Assert.Equal("test-fail", decision.PaymentMethod);
        Assert.False(decision.IsAuthorized);

        Assert.Equal(
            "Simulated payment failure.",
            decision.FailureReason);
    }

    [Fact]
    public void TryProcessUnsupportedMethodReturnsFalse()
    {
        bool recognized = _processor.TryProcess(
            "credit-card",
            out FakePaymentDecision? decision);

        Assert.False(recognized);
        Assert.Null(decision);
    }

    [Fact]
    public void TryProcessBlankMethodReturnsFalse()
    {
        bool recognized = _processor.TryProcess(
            "   ",
            out FakePaymentDecision? decision);

        Assert.False(recognized);
        Assert.Null(decision);
    }
}
