using System.Net;
using System.Net.Http.Json;
using Eshop.Contracts.IntegrationEvents.V1;
using Eshop.Messaging.RabbitMq;
using Eshop.Security.Authorization;
using InventoryService.Application;
using InventoryService.Contracts;
using InventoryService.Data;
using InventoryService.Domain;
using InventoryService.IntegrationTests.Infrastructure;
using InventoryService.Outbox;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryService.IntegrationTests;

public sealed class InventoryServiceIntegrationTests(
    InventoryServiceFixture fixture)
    : IClassFixture<InventoryServiceFixture>,
      IAsyncLifetime
{
    private const string InventoryPath =
        "/api/v1/inventory-items";

    public ValueTask InitializeAsync()
    {
        return fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task
        HealthAnonymousRequestReturnsOk()
    {
        foreach (string endpoint in new[] { "/live", "/ready", "/health" })
        {
            using HttpResponseMessage response =
                await fixture.Client.GetAsync(
                    endpoint,
                    Xunit.TestContext.Current.CancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task
        InventoryAnonymousRequestReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(InventoryPath, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        InventoryCustomerUserReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                InventoryPath,
                CreateSubject("customer"),
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        CreateInventoryItemSupportUserPersistsNormalizedItem()
    {
        Guid productId =
            Guid.NewGuid();

        CreateInventoryItemRequest requestBody = new()
        {
            ProductId = productId,
            Sku = "  inventory-sku-001  ",
            InitialOnHandQuantity = 10,
            IsActive = true
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                InventoryPath,
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        InventoryItemResponse? created =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(productId, created.ProductId);
        Assert.Equal("INVENTORY-SKU-001", created.Sku);
        Assert.Equal(10, created.OnHandQuantity);
        Assert.Equal(0, created.ReservedQuantity);
        Assert.Equal(10, created.AvailableQuantity);
        Assert.True(created.IsActive);
        Assert.Null(created.UpdatedAtUtc);

        Assert.NotNull(response.Headers.Location);

        Assert.EndsWith(
            $"/api/v1/inventory-items/{created.Id}",
            response.Headers.Location.ToString(),
            StringComparison.Ordinal);

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        InventoryDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem persisted =
            await dbContext.InventoryItems
                .AsNoTracking()
                .SingleAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(productId, persisted.ProductId);
        Assert.Equal(created.Sku, persisted.Sku);
        Assert.Equal(10, persisted.OnHandQuantity);
        Assert.Equal(0, persisted.ReservedQuantity);
        Assert.True(persisted.Version > 0);
    }

    [Fact]
    public async Task
        GetInventoryItemByProductIdReturnsPersistedItem()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync();

        using HttpResponseMessage response =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{InventoryPath}/by-product/{created.ProductId}",
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        InventoryItemResponse? item =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(created.ProductId, item.ProductId);
        Assert.Equal(created.Sku, item.Sku);
    }

    [Fact]
    public async Task
        GetInventoryItemsDefaultQueryExcludesInactiveItems()
    {
        InventoryItemResponse activeItem =
            await CreateInventoryItemAsync(
                sku: "ACTIVE-SKU",
                isActive: true);

        InventoryItemResponse inactiveItem =
            await CreateInventoryItemAsync(
                sku: "INACTIVE-SKU",
                isActive: false);

        using HttpResponseMessage defaultResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                InventoryPath,
                EshopRoles.Support);

        InventoryItemResponse[]? activeItems =
            await defaultResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse[]>(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            defaultResponse.StatusCode);

        Assert.NotNull(activeItems);

        Assert.Contains(
            activeItems,
            item => item.Id == activeItem.Id);

        Assert.DoesNotContain(
            activeItems,
            item => item.Id == inactiveItem.Id);

        using HttpResponseMessage allResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{InventoryPath}?includeInactive=true",
                EshopRoles.Support);

        InventoryItemResponse[]? allItems =
            await allResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse[]>(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            allResponse.StatusCode);

        Assert.NotNull(allItems);

        Assert.Contains(
            allItems,
            item => item.Id == activeItem.Id);

        Assert.Contains(
            allItems,
            item => item.Id == inactiveItem.Id);
    }

    [Fact]
    public async Task
        CreateInventoryItemDuplicateProductIdReturnsConflict()
    {
        Guid productId =
            Guid.NewGuid();

        await CreateInventoryItemAsync(
            productId: productId,
            sku: "FIRST-SKU");

        CreateInventoryItemRequest duplicateRequest = new()
        {
            ProductId = productId,
            Sku = "SECOND-SKU",
            InitialOnHandQuantity = 5,
            IsActive = true
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                InventoryPath,
                duplicateRequest,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "Inventory conflict.",
            problem.Title);
    }

    [Fact]
    public async Task
        CreateInventoryItemDuplicateNormalizedSkuReturnsConflict()
    {
        await CreateInventoryItemAsync(
            sku: "DUPLICATE-SKU");

        CreateInventoryItemRequest duplicateRequest = new()
        {
            ProductId = Guid.NewGuid(),
            Sku = "  duplicate-sku  ",
            InitialOnHandQuantity = 5,
            IsActive = true
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                InventoryPath,
                duplicateRequest,
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task
        UpdateInventoryItemValidRequestPersistsChanges()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        UpdateInventoryItemRequest requestBody = new()
        {
            Sku = "  updated-sku  ",
            OnHandQuantity = 15,
            IsActive = false
        };

        using HttpResponseMessage updateResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Put,
                $"{InventoryPath}/{created.Id}",
                requestBody,
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        InventoryItemResponse? updated =
            await updateResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.ProductId, updated.ProductId);
        Assert.Equal("UPDATED-SKU", updated.Sku);
        Assert.Equal(15, updated.OnHandQuantity);
        Assert.Equal(0, updated.ReservedQuantity);
        Assert.Equal(15, updated.AvailableQuantity);
        Assert.False(updated.IsActive);
        Assert.NotNull(updated.UpdatedAtUtc);

        using HttpResponseMessage getResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{InventoryPath}/{created.Id}",
                EshopRoles.Admin);

        InventoryItemResponse? persisted =
            await getResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(updated.Id, persisted.Id);
        Assert.Equal(updated.ProductId, persisted.ProductId);
        Assert.Equal(updated.Sku, persisted.Sku);
        Assert.Equal(
            updated.OnHandQuantity,
            persisted.OnHandQuantity);

        Assert.Equal(
            updated.ReservedQuantity,
            persisted.ReservedQuantity);

        Assert.Equal(
            updated.AvailableQuantity,
            persisted.AvailableQuantity);

        Assert.Equal(updated.IsActive, persisted.IsActive);
    }

    [Fact]
    public async Task
        StockAdjustmentOperationPersistsAuditSnapshot()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        Guid idempotencyKey = Guid.NewGuid();

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        InventoryDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem item =
            await dbContext.InventoryItems
                .SingleAsync(
                    candidate => candidate.Id == created.Id,
                    Xunit.TestContext.Current.CancellationToken);

        int onHandBefore = item.OnHandQuantity;
        int reservedBefore = item.ReservedQuantity;
        int availableBefore = item.AvailableQuantity;

        InventoryStockAdjustmentOperation operation =
            InventoryStockAdjustmentOperation.Begin(
                idempotencyKey,
                item.Id,
                quantityDelta: 2,
                expectedVersion: item.Version,
                reason: "Physical stock count correction",
                actorSubject: "admin-123",
                actorUsername: "anna.admin",
                traceId: "trace-123",
                occurredAtUtc: DateTimeOffset.UtcNow);

        item.AdjustOnHandQuantity(
            quantityDelta: 2,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(
            Xunit.TestContext.Current.CancellationToken);

        operation.CompleteSuccess(
            item,
            onHandBefore,
            reservedBefore,
            availableBefore);

        dbContext.InventoryStockAdjustmentOperations.Add(operation);

        await dbContext.SaveChangesAsync(
            Xunit.TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();

        InventoryStockAdjustmentOperation persisted =
            await dbContext.InventoryStockAdjustmentOperations
                .AsNoTracking()
                .SingleAsync(
                    candidate => candidate.IdempotencyKey == idempotencyKey,
                    Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            InventoryStockAdjustmentOutcome.Success,
            persisted.Outcome);
        Assert.Equal(created.Id, persisted.InventoryItemId);
        Assert.Equal(2, persisted.QuantityDelta);
        Assert.Equal(
            "Physical stock count correction",
            persisted.Reason);
        Assert.Equal("admin-123", persisted.ActorSubject);
        Assert.Equal("anna.admin", persisted.ActorUsername);
        Assert.Equal(10, persisted.OnHandBefore);
        Assert.Equal(12, persisted.OnHandAfter);
        Assert.Equal(item.Version, persisted.ResultVersion);
    }

    [Fact]
    public async Task
        StockAdjustmentOperationRejectsDuplicateIdempotencyKey()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        Guid idempotencyKey = Guid.NewGuid();

        await using AsyncServiceScope scope =
            fixture.Factory.Services
                .CreateAsyncScope();

        InventoryDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryStockAdjustmentOperation first =
            InventoryStockAdjustmentOperation.Begin(
                idempotencyKey,
                created.Id,
                quantityDelta: 1,
                expectedVersion: created.Version,
                reason: "First adjustment",
                actorSubject: "admin-123",
                actorUsername: "anna.admin",
                traceId: null,
                occurredAtUtc: DateTimeOffset.UtcNow);

        first.CompleteFailure(
            InventoryStockAdjustmentOutcome.Conflict,
            "Test operation.");

        dbContext.InventoryStockAdjustmentOperations.Add(first);

        await dbContext.SaveChangesAsync(
            Xunit.TestContext.Current.CancellationToken);

        InventoryStockAdjustmentOperation duplicate =
            InventoryStockAdjustmentOperation.Begin(
                idempotencyKey,
                created.Id,
                quantityDelta: 1,
                expectedVersion: created.Version,
                reason: "First adjustment",
                actorSubject: "admin-123",
                actorUsername: "anna.admin",
                traceId: null,
                occurredAtUtc: DateTimeOffset.UtcNow);

        duplicate.CompleteFailure(
            InventoryStockAdjustmentOutcome.Conflict,
            "Test operation.");

        dbContext.InventoryStockAdjustmentOperations.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync(
                Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task
        AdjustInventoryStockValidDeltaPersistsNewQuantity()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = 5,
            ExpectedVersion = created.Version
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        InventoryItemResponse? adjusted =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(adjusted);
        Assert.Equal(15, adjusted.OnHandQuantity);
        Assert.Equal(0, adjusted.ReservedQuantity);
        Assert.Equal(15, adjusted.AvailableQuantity);
        Assert.NotNull(adjusted.UpdatedAtUtc);
        Assert.NotEqual(created.Version, adjusted.Version);
    }

    [Fact]
    public async Task
        AdjustInventoryStockZeroDeltaReturnsValidationProblem()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync();

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = 0,
            ExpectedVersion = created.Version
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ValidationProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        Assert.Contains(
            nameof(AdjustInventoryStockRequest.QuantityDelta),
            problem.Errors.Keys);
    }

    [Fact]
    public async Task
        AdjustInventoryStockZeroExpectedVersionReturnsValidationProblem()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync();

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = 1,
            ExpectedVersion = 0
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ValidationProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Contains(
            nameof(AdjustInventoryStockRequest.ExpectedVersion),
            problem.Errors.Keys);
    }

    [Fact]
    public async Task
        AdjustInventoryStockBelowReservedQuantityReturnsBadRequestWithoutMutation()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        uint currentVersion;

        await using (
            AsyncServiceScope setupScope =
                fixture.Factory.Services
                    .CreateAsyncScope())
        {
            InventoryDbContext dbContext =
                setupScope.ServiceProvider
                    .GetRequiredService<InventoryDbContext>();

            InventoryItem item =
                await dbContext.InventoryItems
                    .SingleAsync(
                        candidate =>
                            candidate.Id == created.Id,
                        Xunit.TestContext.Current.CancellationToken);

            Assert.True(
                item.TryReserve(
                    quantity: 6,
                    DateTimeOffset.UtcNow));

            await dbContext.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
            currentVersion = item.Version;
        }

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = -5,
            ExpectedVersion = currentVersion
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>(Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "Inventory validation failed.",
            problem.Title);

        await using AsyncServiceScope verificationScope =
            fixture.Factory.Services
                .CreateAsyncScope();

        InventoryDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem persisted =
            await verificationContext.InventoryItems
                .AsNoTracking()
                .SingleAsync(
                    candidate =>
                        candidate.Id == created.Id,
                    Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(10, persisted.OnHandQuantity);
        Assert.Equal(6, persisted.ReservedQuantity);
        Assert.Equal(4, persisted.AvailableQuantity);
    }

    [Fact]
    public async Task
        MissingInventoryItemOperationsReturnNotFound()
    {
        Guid missingId =
            Guid.NewGuid();

        using HttpResponseMessage getResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{InventoryPath}/{missingId}",
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);

        UpdateInventoryItemRequest updateRequest = new()
        {
            Sku = "MISSING-SKU",
            OnHandQuantity = 10,
            IsActive = true
        };

        using HttpResponseMessage updateResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Put,
                $"{InventoryPath}/{missingId}",
                updateRequest,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode);

        AdjustInventoryStockRequest adjustmentRequest = new()
        {
            QuantityDelta = 1,
            ExpectedVersion = 1
        };

        using HttpResponseMessage adjustmentResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{missingId}/stock-adjustments",
                adjustmentRequest,
                EshopRoles.Admin);

        Assert.Equal(
            HttpStatusCode.NotFound,
            adjustmentResponse.StatusCode);
    }

    [Fact]
    public async Task
        AdjustInventoryStockSupportRoleReturnsForbidden()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = 1,
            ExpectedVersion = created.Version
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Support);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task
        AdjustInventoryStockStaleVersionReturnsConflictWithoutSecondMutation()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        AdjustInventoryStockRequest firstRequest = new()
        {
            QuantityDelta = 2,
            ExpectedVersion = created.Version
        };

        using HttpResponseMessage firstResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                firstRequest,
                EshopRoles.Admin);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        InventoryItemResponse? firstAdjustment =
            await firstResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(firstAdjustment);
        Assert.Equal(12, firstAdjustment.OnHandQuantity);
        Assert.NotEqual(created.Version, firstAdjustment.Version);

        AdjustInventoryStockRequest staleRequest = new()
        {
            QuantityDelta = 3,
            ExpectedVersion = created.Version
        };

        using HttpResponseMessage staleResponse =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                staleRequest,
                EshopRoles.Admin);

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        ProblemDetails? problem =
            await staleResponse.Content
                .ReadFromJsonAsync<ProblemDetails>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal("Inventory conflict.", problem.Title);

        using HttpResponseMessage getResponse =
            await SendAuthenticatedAsync(
                HttpMethod.Get,
                $"{InventoryPath}/{created.Id}",
                EshopRoles.Admin);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        InventoryItemResponse? persisted =
            await getResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(12, persisted.OnHandQuantity);
        Assert.Equal(firstAdjustment.Version, persisted.Version);
    }

    [Fact]
    public async Task
        ConcurrentReservationsForLastUnitDoNotOversellAndRetryLoser()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 1);

        OrderCreatedV1 firstOrder =
            CreateOrderCreatedEvent(
                (created.ProductId, 1));

        OrderCreatedV1 secondOrder =
            CreateOrderCreatedEvent(
                (created.ProductId, 1));

        CoordinatedFirstSaveInterceptor coordinator =
            new(requiredParticipants: 2);

        ReserveOrderStockResult[] results =
            await ExecuteConcurrentReservationsAsync(
                coordinator,
                firstOrder,
                secondOrder);

        Assert.Equal(
            1,
            results.Count(result =>
                result.Status
                    == ReserveOrderStockStatus.Reserved));

        Assert.Equal(
            1,
            results.Count(result =>
                result.Status
                    == ReserveOrderStockStatus.Failed));

        Assert.Equal(2, coordinator.FirstWaveArrivals);
        Assert.Equal(3, coordinator.SaveAttemptCount);

        await using AsyncServiceScope verificationScope =
            fixture.Factory.Services.CreateAsyncScope();

        InventoryDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem persisted =
            await verificationContext.InventoryItems
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == created.Id,
                    Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, persisted.OnHandQuantity);
        Assert.Equal(1, persisted.ReservedQuantity);
        Assert.Equal(0, persisted.AvailableQuantity);

        Assert.Equal(
            2,
            await verificationContext.ProcessedMessages
                .CountAsync(Xunit.TestContext.Current.CancellationToken));

        string[] routingKeys =
            await verificationContext.OutboxMessages
                .AsNoTracking()
                .OrderBy(message => message.RoutingKey)
                .Select(message => message.RoutingKey)
                .ToArrayAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                RabbitMqRoutingKeys.StockReservationFailedV1,
                RabbitMqRoutingKeys.StockReservedV1
            ],
            routingKeys);
    }

    [Fact]
    public async Task
        ConcurrentMultiLineReservationsDoNotPartiallyReserveLosingOrder()
    {
        InventoryItemResponse constrainedItem =
            await CreateInventoryItemAsync(
                sku: "CONSTRAINED-SKU",
                initialOnHandQuantity: 1);

        InventoryItemResponse sharedItem =
            await CreateInventoryItemAsync(
                sku: "SHARED-SKU",
                initialOnHandQuantity: 2);

        OrderCreatedV1 firstOrder =
            CreateOrderCreatedEvent(
                (constrainedItem.ProductId, 1),
                (sharedItem.ProductId, 1));

        OrderCreatedV1 secondOrder =
            CreateOrderCreatedEvent(
                (constrainedItem.ProductId, 1),
                (sharedItem.ProductId, 1));

        CoordinatedFirstSaveInterceptor coordinator =
            new(requiredParticipants: 2);

        ReserveOrderStockResult[] results =
            await ExecuteConcurrentReservationsAsync(
                coordinator,
                firstOrder,
                secondOrder);

        Assert.Equal(
            1,
            results.Count(result =>
                result.Status
                    == ReserveOrderStockStatus.Reserved));

        Assert.Equal(
            1,
            results.Count(result =>
                result.Status
                    == ReserveOrderStockStatus.Failed));

        Assert.Equal(2, coordinator.FirstWaveArrivals);
        Assert.Equal(3, coordinator.SaveAttemptCount);

        await using AsyncServiceScope verificationScope =
            fixture.Factory.Services.CreateAsyncScope();

        InventoryDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem[] persistedItems =
            await verificationContext.InventoryItems
                .AsNoTracking()
                .OrderBy(item => item.Sku)
                .ToArrayAsync(Xunit.TestContext.Current.CancellationToken);

        InventoryItem persistedConstrainedItem =
            Assert.Single(
                persistedItems,
                item =>
                    item.ProductId
                        == constrainedItem.ProductId);

        InventoryItem persistedSharedItem =
            Assert.Single(
                persistedItems,
                item =>
                    item.ProductId
                        == sharedItem.ProductId);

        Assert.Equal(
            1,
            persistedConstrainedItem.ReservedQuantity);

        Assert.Equal(
            0,
            persistedConstrainedItem.AvailableQuantity);

        Assert.Equal(1, persistedSharedItem.ReservedQuantity);
        Assert.Equal(1, persistedSharedItem.AvailableQuantity);

        Assert.Equal(
            2,
            await verificationContext.ProcessedMessages
                .CountAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            2,
            await verificationContext.OutboxMessages
                .CountAsync(Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task
        ReservationConcurrencyRetryExhaustionLeavesDatabaseUnchanged()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 1);

        AlwaysConcurrencyConflictInterceptor interceptor =
            new();

        await using InventoryDbContext context =
            CreateInventoryDbContext(interceptor);

        OrderStockReservationService service =
            new(
                context,
                new InventoryOutboxWriter(),
                TimeProvider.System);

        OrderCreatedV1 orderCreated =
            CreateOrderCreatedEvent(
                (created.ProductId, 1));

        DbUpdateConcurrencyException exception =
            await Assert.ThrowsAsync<
                DbUpdateConcurrencyException>(
                () => service.ReserveAsync(
                    orderCreated,
                    Xunit.TestContext.Current.CancellationToken));

        Assert.Contains(
            "after 3 concurrency attempts",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Equal(3, interceptor.SaveAttemptCount);

        await using AsyncServiceScope verificationScope =
            fixture.Factory.Services.CreateAsyncScope();

        InventoryDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem persisted =
            await verificationContext.InventoryItems
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == created.Id,
                    Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, persisted.OnHandQuantity);
        Assert.Equal(0, persisted.ReservedQuantity);
        Assert.Equal(1, persisted.AvailableQuantity);

        Assert.Equal(
            0,
            await verificationContext.ProcessedMessages
                .CountAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            0,
            await verificationContext.OutboxMessages
                .CountAsync(Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task
        InventoryRowVersionConcurrentUpdatesRejectStaleWrite()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        await using AsyncServiceScope firstScope =
            fixture.Factory.Services.CreateAsyncScope();

        await using AsyncServiceScope secondScope =
            fixture.Factory.Services.CreateAsyncScope();

        InventoryDbContext firstContext =
            firstScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryDbContext secondContext =
            secondScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem firstEntity =
            await firstContext.InventoryItems
                .SingleAsync(
                    item => item.Id == created.Id,
                Xunit.TestContext.Current.CancellationToken);

        InventoryItem secondEntity =
            await secondContext.InventoryItems
                .SingleAsync(
                    item => item.Id == created.Id,
                Xunit.TestContext.Current.CancellationToken);

        firstEntity.AdjustOnHandQuantity(
            quantityDelta: 1,
            DateTimeOffset.UtcNow);

        secondEntity.AdjustOnHandQuantity(
            quantityDelta: 1,
            DateTimeOffset.UtcNow);

        await firstContext.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken));

        await using AsyncServiceScope verificationScope =
            fixture.Factory.Services.CreateAsyncScope();

        InventoryDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem persisted =
            await verificationContext.InventoryItems
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == created.Id,
                Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(11, persisted.OnHandQuantity);
    }

    private async Task<ReserveOrderStockResult[]>
        ExecuteConcurrentReservationsAsync(
            SaveChangesInterceptor interceptor,
            OrderCreatedV1 firstOrder,
            OrderCreatedV1 secondOrder)
    {
        await using InventoryDbContext firstContext =
            CreateInventoryDbContext(interceptor);

        await using InventoryDbContext secondContext =
            CreateInventoryDbContext(interceptor);

        OrderStockReservationService firstService =
            new(
                firstContext,
                new InventoryOutboxWriter(),
                TimeProvider.System);

        OrderStockReservationService secondService =
            new(
                secondContext,
                new InventoryOutboxWriter(),
                TimeProvider.System);

        Task<ReserveOrderStockResult> firstReservation =
            firstService.ReserveAsync(
                firstOrder,
                Xunit.TestContext.Current.CancellationToken);

        Task<ReserveOrderStockResult> secondReservation =
            secondService.ReserveAsync(
                secondOrder,
                Xunit.TestContext.Current.CancellationToken);

        return await Task.WhenAll(
            firstReservation,
            secondReservation);
    }

    private InventoryDbContext CreateInventoryDbContext(
        SaveChangesInterceptor interceptor)
    {
        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseNpgsql(
                    fixture.PostgresConnectionString)
                .AddInterceptors(interceptor)
                .Options;

        return new InventoryDbContext(options);
    }

    private static OrderCreatedV1 CreateOrderCreatedEvent(
        params (Guid ProductId, int Quantity)[] items)
    {
        DateTimeOffset occurredAtUtc =
            DateTimeOffset.UtcNow;

        return new OrderCreatedV1(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: occurredAtUtc,
            CorrelationId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            CustomerId: $"customer-{Guid.NewGuid():N}",
            TotalAmount: items.Sum(item =>
                item.Quantity * 10m),
            Currency: "CZK",
            Items: items
                .Select(item =>
                    new OrderCreatedItemV1(
                        item.ProductId,
                        ProductName:
                            $"Product-{item.ProductId:N}",
                        item.Quantity,
                        UnitPrice: 10m))
                .ToArray());
    }

    private sealed class CoordinatedFirstSaveInterceptor(
        int requiredParticipants)
        : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _release =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private int _firstWaveArrivals;
        private int _saveAttemptCount;

        public int FirstWaveArrivals =>
            Volatile.Read(ref _firstWaveArrivals);

        public int SaveAttemptCount =>
            Volatile.Read(ref _saveAttemptCount);

        public override async ValueTask<
            InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            int saveAttempt =
                Interlocked.Increment(
                    ref _saveAttemptCount);

            if (saveAttempt > requiredParticipants)
            {
                return result;
            }

            int arrivals =
                Interlocked.Increment(
                    ref _firstWaveArrivals);

            if (arrivals == requiredParticipants)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            return result;
        }
    }

    private sealed class AlwaysConcurrencyConflictInterceptor
        : SaveChangesInterceptor
    {
        private int _saveAttemptCount;

        public int SaveAttemptCount =>
            Volatile.Read(ref _saveAttemptCount);

        public override ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(
                ref _saveAttemptCount);

            throw new DbUpdateConcurrencyException(
                "Injected deterministic concurrency conflict.");
        }
    }

    private async Task<InventoryItemResponse>
        CreateInventoryItemAsync(
            Guid? productId = null,
            string? sku = null,
            int initialOnHandQuantity = 10,
            bool isActive = true)
    {
        CreateInventoryItemRequest requestBody = new()
        {
            ProductId =
                productId ?? Guid.NewGuid(),

            Sku =
                sku ?? $"SKU-{Guid.NewGuid():N}",

            InitialOnHandQuantity =
                initialOnHandQuantity,

            IsActive =
                isActive
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                InventoryPath,
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        InventoryItemResponse? created =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>(Xunit.TestContext.Current.CancellationToken);

        return Assert.IsType<InventoryItemResponse>(
            created);
    }

    private async Task<HttpResponseMessage>
        SendAuthenticatedAsync(
            HttpMethod method,
            string path,
            string role)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                method,
                path,
                CreateSubject(role),
                role);

        return await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage>
        SendAuthenticatedJsonAsync<TBody>(
            HttpMethod method,
            string path,
            TBody body,
            string role)
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                method,
                path,
                CreateSubject(role),
                role);

        request.Content =
            JsonContent.Create(body);

        return await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage
        CreateAuthenticatedRequest(
            HttpMethod method,
            string path,
            string subject,
            params string[] roles)
    {
        HttpRequestMessage request =
            new(method, path);

        request.Headers.Add(
            TestAuthenticationHandler.SubjectHeaderName,
            subject);

        request.Headers.Add(
            TestAuthenticationHandler.RolesHeaderName,
            string.Join(',', roles));

        return request;
    }

    private static string CreateSubject(
        string scenario)
    {
        return $"{scenario}-{Guid.NewGuid():N}";
    }
}
