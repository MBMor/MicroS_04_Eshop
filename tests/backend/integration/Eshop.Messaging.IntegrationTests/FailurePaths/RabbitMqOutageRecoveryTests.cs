using System.Net;
using System.Net.Http.Json;
using Eshop.Messaging.IntegrationTests.Infrastructure;
using Eshop.Messaging.IntegrationTests.Infrastructure.Fakes;
using Messaging.Shared.RabbitMq;
using Microsoft.EntityFrameworkCore;
using OrdersService.Contracts;
using OrdersService.Data;
using OrdersService.Domain;
using OrdersService.Integration;
using OrdersService.Outbox;
using PaymentsService.Application;
using Xunit;

namespace Eshop.Messaging.IntegrationTests.FailurePaths;

[Collection(MessagingTestCollections.System)]
public sealed class RabbitMqOutageRecoveryTests(
    MessagingSystemFixture fixture)
    : MessagingIntegrationTestBase(fixture)
{
    private const string CustomerId =
        "rabbitmq-outage-customer";

    private const string CustomerEmail =
        "rabbitmq-outage@example.test";

    private static readonly TimeSpan ScenarioTimeout =
        TimeSpan.FromSeconds(60);

    [Fact]
    public async Task OrdersOutbox_RabbitMqOutage_RetriesAndPublishesAfterRecovery()
    {
        Guid productId =
            Guid.NewGuid();

        const int orderedQuantity = 2;
        const decimal unitPrice = 49.90m;

        Fixture.OrdersFactory.BasketClient.SetBasket(
            CustomerId,
            new BasketSnapshot(
            [
                new BasketItemSnapshot(
                ProductId: productId,
                ProductName:
                    "RabbitMQ Outage Test Product",
                UnitPrice: unitPrice,
                Currency: "CZK",
                Quantity: orderedQuantity,
                LineTotal:
                    unitPrice * orderedQuantity)
            ]));

        bool rabbitMqIsRunning = true;
        bool serviceHostsWereRestarted = false;

        try
        {
            await Fixture.StopRabbitMqAsync();

            rabbitMqIsRunning = false;

            OrderResponse createdOrder =
                await CreateOrderAsync();

            Assert.NotEqual(
                Guid.Empty,
                createdOrder.Id);

            Assert.Equal(
                OrderStatus.PendingStockReservation
                    .ToString(),
                createdOrder.Status);

            Assert.True(
                Fixture.OrdersFactory.BasketClient
                    .WasCleared(CustomerId));

            OrdersOutboxSnapshot retrySnapshot =
                await WaitForFailedPublishAttemptAsync(
                    createdOrder.Id);

            Assert.True(
                retrySnapshot.RetryCount >= 1);

            Assert.False(
                string.IsNullOrWhiteSpace(
                    retrySnapshot.LastError));

            Assert.Null(
                retrySnapshot.PublishedAtUtc);

            Assert.NotEqual(
                OutboxMessageStatus.Pending,
                retrySnapshot.Status);

            Assert.NotEqual(
                OutboxMessageStatus.Published,
                retrySnapshot.Status);

            Assert.NotEqual(
                OutboxMessageStatus.Dead,
                retrySnapshot.Status);

            await Fixture.StartRabbitMqAsync();

            rabbitMqIsRunning = true;

            // The previous publisher may be blocked inside
            // a publisher-confirm operation on the original connection.
            // Restarting the service hosts creates a new connection provider
            // and a new outbox worker.
            await Fixture.RestartServiceHostsAsync(
                CancellationToken.None);

            serviceHostsWereRestarted = true;

            // After the original worker was stopped, the message may
            // have remained in the Processing state. The new worker
            // will claim it after Outbox:ClaimTimeout expires.

            await AssertOutboxMessageWasPublishedAsync(
                retrySnapshot.Id);
        }
        finally
        {
            if (!rabbitMqIsRunning)
            {
                await Fixture.StartRabbitMqAsync(
                    CancellationToken.None);
            }

            if (!serviceHostsWereRestarted)
            {
                await Fixture.RestartServiceHostsAsync(
                    CancellationToken.None);
            }
        }
    }

    private async Task<OrderResponse> CreateOrderAsync()
    {
        using HttpClient client =
            Fixture.OrdersFactory.CreateClient();

        client.DefaultRequestHeaders.Add(
            TestOrderOwnerProvider.CustomerIdHeaderName,
            CustomerId);

        CreateOrderRequest request =
            new()
            {
                CustomerEmail = CustomerEmail,
                PaymentMethod =
                    FakePaymentProcessor.SuccessMethod
            };

        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/api/v1/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        OrderResponse? order =
            await response.Content
                .ReadFromJsonAsync<OrderResponse>();

        return Assert.IsType<OrderResponse>(
            order);
    }

    private async Task<OrdersOutboxSnapshot>
        WaitForFailedPublishAttemptAsync(
            Guid orderId)
    {
        OrdersOutboxSnapshot? result =
            null;

        await Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OrdersOutboxSnapshot? snapshot =
                    await LoadOrderCreatedOutboxAsync(
                        orderId,
                        cancellationToken);

                OrdersOutboxSnapshot existingSnapshot =
                    Assert.IsType<OrdersOutboxSnapshot>(
                        snapshot);

                Assert.True(
                    existingSnapshot.RetryCount >= 1,
                    "The outbox message has not recorded " +
                    "a failed publish attempt yet.");

                Assert.False(
                    string.IsNullOrWhiteSpace(
                        existingSnapshot.LastError));

                Assert.Null(
                    existingSnapshot.PublishedAtUtc);

                Assert.NotEqual(
                    OutboxMessageStatus.Pending,
                    existingSnapshot.Status);

                Assert.NotEqual(
                    OutboxMessageStatus.Published,
                    existingSnapshot.Status);

                Assert.NotEqual(
                    OutboxMessageStatus.Dead,
                    existingSnapshot.Status);

                result =
                    existingSnapshot;
            },
            $"OrderCreated outbox message for order " +
            $"'{orderId}' should record a failed publish " +
            $"attempt while RabbitMQ is unavailable.",
            timeout: ScenarioTimeout);

        return Assert.IsType<OrdersOutboxSnapshot>(
            result);
    }

    private Task AssertOutboxMessageWasPublishedAsync(
        Guid outboxMessageId)
    {
        return Eventually.SucceedsAsync(
            async cancellationToken =>
            {
                OrdersOutboxSnapshot snapshot =
                    await LoadOutboxByIdAsync(
                        outboxMessageId,
                        cancellationToken);

                Assert.Equal(
                    OutboxMessageStatus.Published,
                    snapshot.Status);

                // RetryCount zůstává zachovaný jako důkaz,
                // že před úspěšným publish proběhla chyba.
                Assert.True(
                    snapshot.RetryCount >= 1);

                Assert.NotNull(
                    snapshot.PublishedAtUtc);

                Assert.Null(
                    snapshot.LastError);

                Assert.Null(
                    snapshot.NextAttemptAtUtc);

                Assert.Null(
                    snapshot.ClaimedAtUtc);

                Assert.Null(
                    snapshot.ClaimedBy);
            },
            $"Outbox message '{outboxMessageId}' should " +
            "be published after RabbitMQ recovery.",
            timeout: ScenarioTimeout);
    }

    private async Task<OrdersOutboxSnapshot?>
        LoadOrderCreatedOutboxAsync(
            Guid orderId,
            CancellationToken cancellationToken)
    {
        string orderIdText =
            orderId.ToString("D");

        return await DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            OrdersOutboxSnapshot?>(
            Fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                OutboxMessageCandidate[] candidates =
                    await dbContext.OutboxMessages
                        .AsNoTracking()
                        .Where(message =>
                            message.RoutingKey
                                == RabbitMqRoutingKeys
                                    .OrderCreatedV1)
                        .Select(message =>
                            new OutboxMessageCandidate(
                                message.Id,
                                message.Payload,
                                message.Status,
                                message.RetryCount,
                                message.LastError,
                                message.PublishedAtUtc,
                                message.NextAttemptAtUtc,
                                message.ClaimedAtUtc,
                                message.ClaimedBy))
                        .ToArrayAsync(token);

                OutboxMessageCandidate? candidate =
                    candidates.SingleOrDefault(
                        message =>
                            message.Payload.Contains(
                                orderIdText,
                                StringComparison.OrdinalIgnoreCase));

                return candidate is null
                    ? null
                    : new OrdersOutboxSnapshot(
                        candidate.Id,
                        candidate.Status,
                        candidate.RetryCount,
                        candidate.LastError,
                        candidate.PublishedAtUtc,
                        candidate.NextAttemptAtUtc,
                        candidate.ClaimedAtUtc,
                        candidate.ClaimedBy);
            },
            cancellationToken);
    }

    private Task<OrdersOutboxSnapshot>
        LoadOutboxByIdAsync(
            Guid outboxMessageId,
            CancellationToken cancellationToken)
    {
        return DatabaseTestScope.ExecuteAsync<
            OrdersDbContext,
            OrdersOutboxSnapshot>(
            Fixture.OrdersFactory.Services,
            async (dbContext, token) =>
            {
                return await dbContext.OutboxMessages
                    .AsNoTracking()
                    .Where(message =>
                        message.Id == outboxMessageId)
                    .Select(message =>
                        new OrdersOutboxSnapshot(
                            message.Id,
                            message.Status,
                            message.RetryCount,
                            message.LastError,
                            message.PublishedAtUtc,
                            message.NextAttemptAtUtc,
                            message.ClaimedAtUtc,
                            message.ClaimedBy))
                    .SingleAsync(token);
            },
            cancellationToken);
    }

    private sealed record OutboxMessageCandidate(
    Guid Id,
    string Payload,
    OutboxMessageStatus Status,
    int RetryCount,
    string? LastError,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    string? ClaimedBy);

    private sealed record OrdersOutboxSnapshot(
        Guid Id,
        OutboxMessageStatus Status,
        int RetryCount,
        string? LastError,
        DateTimeOffset? PublishedAtUtc,
        DateTimeOffset? NextAttemptAtUtc,
        DateTimeOffset? ClaimedAtUtc,
        string? ClaimedBy);
}
