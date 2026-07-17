using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Domain;
using PaymentsService.Outbox;

namespace PaymentsService.Application;

public sealed class PaymentRequestedProcessingService(
    PaymentsDbContext dbContext,
    FakePaymentProcessor fakePaymentProcessor,
    PaymentsOutboxWriter outboxWriter,
    TimeProvider timeProvider)
{
    public async Task ProcessAsync(
        PaymentRequestedV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        bool paymentAlreadyExists = await dbContext.Payments
            .AnyAsync(
                payment => payment.OrderId == integrationEvent.OrderId,
                cancellationToken);

        if (paymentAlreadyExists)
        {
            return;
        }

        if (!fakePaymentProcessor.TryProcess(
                integrationEvent.PaymentMethod,
                out FakePaymentDecision? decision)
            || decision is null)
        {
            await CreateFailedPaymentAsync(
                integrationEvent,
                "Unsupported payment method.",
                cancellationToken);

            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Payment payment = Payment.CreatePending(
            id: Guid.NewGuid(),
            orderId: integrationEvent.OrderId,
            customerId: integrationEvent.CustomerId,
            amount: integrationEvent.Amount,
            currency: integrationEvent.Currency,
            paymentMethod: decision.PaymentMethod,
            createdAtUtc: now);

        dbContext.Payments.Add(payment);

        if (decision.IsAuthorized)
        {
            payment.Authorize(now);

            PaymentAuthorizedV1 paymentAuthorized = new(
                EventId: Guid.NewGuid(),
                OccurredAtUtc: now,
                CorrelationId: integrationEvent.CorrelationId,
                OrderId: integrationEvent.OrderId,
                PaymentId: payment.Id,
                CustomerId: integrationEvent.CustomerId,
                Amount: integrationEvent.Amount,
                Currency: integrationEvent.Currency);

            dbContext.OutboxMessages.Add(
                outboxWriter.Create(
                    paymentAuthorized,
                    RabbitMqRoutingKeys.PaymentAuthorizedV1));
        }
        else
        {
            string failureReason =
                decision.FailureReason
                ?? "Simulated payment failure.";

            payment.Fail(failureReason, now);

            PaymentFailedV1 paymentFailed = new(
                EventId: Guid.NewGuid(),
                OccurredAtUtc: now,
                CorrelationId: integrationEvent.CorrelationId,
                OrderId: integrationEvent.OrderId,
                PaymentId: payment.Id,
                CustomerId: integrationEvent.CustomerId,
                Amount: integrationEvent.Amount,
                Currency: integrationEvent.Currency,
                Reason: failureReason);

            dbContext.OutboxMessages.Add(
                outboxWriter.Create(
                    paymentFailed,
                    RabbitMqRoutingKeys.PaymentFailedV1));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateFailedPaymentAsync(
        PaymentRequestedV1 integrationEvent,
        string failureReason,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        Payment payment = Payment.CreatePending(
            id: Guid.NewGuid(),
            orderId: integrationEvent.OrderId,
            customerId: integrationEvent.CustomerId,
            amount: integrationEvent.Amount,
            currency: integrationEvent.Currency,
            paymentMethod: integrationEvent.PaymentMethod,
            createdAtUtc: now);

        payment.Fail(failureReason, now);

        PaymentFailedV1 paymentFailed = new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: now,
            CorrelationId: integrationEvent.CorrelationId,
            OrderId: integrationEvent.OrderId,
            PaymentId: payment.Id,
            CustomerId: integrationEvent.CustomerId,
            Amount: integrationEvent.Amount,
            Currency: integrationEvent.Currency,
            Reason: failureReason);

        dbContext.Payments.Add(payment);

        dbContext.OutboxMessages.Add(
            outboxWriter.Create(
                paymentFailed,
                RabbitMqRoutingKeys.PaymentFailedV1));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
