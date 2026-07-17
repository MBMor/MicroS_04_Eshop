using Eshop.Contracts.IntegrationEvents.V1;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Domain;
using PaymentsService.Inbox;
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
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(
                message =>
                    message.EventId == integrationEvent.EventId
                    && message.ConsumerName == ConsumerNames.PaymentRequested,
                cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        bool paymentAlreadyExists = await dbContext.Payments
            .AnyAsync(
                payment => payment.OrderId == integrationEvent.OrderId,
                cancellationToken);

        if (paymentAlreadyExists)
        {
            dbContext.ProcessedMessages.Add(
                ProcessedMessage.Create(
                    integrationEvent.EventId,
                    ConsumerNames.PaymentRequested,
                    timeProvider.GetUtcNow()));

            await dbContext.SaveChangesAsync(cancellationToken);

            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (!fakePaymentProcessor.TryProcess(
                integrationEvent.PaymentMethod,
                out FakePaymentDecision? decision)
            || decision is null)
        {
            await CreateFailedPaymentAsync(
                integrationEvent,
                "Unsupported payment method.",
                now,
                cancellationToken);

            return;
        }

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

            payment.Fail(
                failureReason,
                now);

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

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.PaymentRequested,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateFailedPaymentAsync(
        PaymentRequestedV1 integrationEvent,
        string failureReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Payment payment = Payment.CreatePending(
            id: Guid.NewGuid(),
            orderId: integrationEvent.OrderId,
            customerId: integrationEvent.CustomerId,
            amount: integrationEvent.Amount,
            currency: integrationEvent.Currency,
            paymentMethod: integrationEvent.PaymentMethod,
            createdAtUtc: now);

        payment.Fail(
            failureReason,
            now);

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

        dbContext.ProcessedMessages.Add(
            ProcessedMessage.Create(
                integrationEvent.EventId,
                ConsumerNames.PaymentRequested,
                now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
