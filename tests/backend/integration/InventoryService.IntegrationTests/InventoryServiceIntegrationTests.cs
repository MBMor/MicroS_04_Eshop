using System.Net;
using System.Net.Http.Json;
using Eshop.Security.Authorization;
using InventoryService.Contracts;
using InventoryService.Data;
using InventoryService.Domain;
using InventoryService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        Health_AnonymousRequest_ReturnsOk()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
        Inventory_AnonymousRequest_ReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(InventoryPath);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        Inventory_CustomerUser_ReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                InventoryPath,
                CreateSubject("customer"),
                EshopRoles.Customer);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        CreateInventoryItem_SupportUser_PersistsNormalizedItem()
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
                .ReadFromJsonAsync<InventoryItemResponse>();

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
                .SingleAsync();

        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(productId, persisted.ProductId);
        Assert.Equal(created.Sku, persisted.Sku);
        Assert.Equal(10, persisted.OnHandQuantity);
        Assert.Equal(0, persisted.ReservedQuantity);
        Assert.True(persisted.Version > 0);
    }

    [Fact]
    public async Task
        GetInventoryItemByProductId_ReturnsPersistedItem()
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
                .ReadFromJsonAsync<InventoryItemResponse>();

        Assert.NotNull(item);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(created.ProductId, item.ProductId);
        Assert.Equal(created.Sku, item.Sku);
    }

    [Fact]
    public async Task
        GetInventoryItems_DefaultQueryExcludesInactiveItems()
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
                .ReadFromJsonAsync<InventoryItemResponse[]>();

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
                .ReadFromJsonAsync<InventoryItemResponse[]>();

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
        CreateInventoryItem_DuplicateProductId_ReturnsConflict()
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
                .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);

        Assert.Equal(
            "Inventory conflict.",
            problem.Title);
    }

    [Fact]
    public async Task
        CreateInventoryItem_DuplicateNormalizedSku_ReturnsConflict()
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
        UpdateInventoryItem_ValidRequest_PersistsChanges()
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
                .ReadFromJsonAsync<InventoryItemResponse>();

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
                .ReadFromJsonAsync<InventoryItemResponse>();

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
        AdjustInventoryStock_ValidDelta_PersistsNewQuantity()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = 5
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        InventoryItemResponse? adjusted =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>();

        Assert.NotNull(adjusted);
        Assert.Equal(15, adjusted.OnHandQuantity);
        Assert.Equal(0, adjusted.ReservedQuantity);
        Assert.Equal(15, adjusted.AvailableQuantity);
        Assert.NotNull(adjusted.UpdatedAtUtc);
    }

    [Fact]
    public async Task
        AdjustInventoryStock_ZeroDelta_ReturnsValidationProblem()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync();

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = 0
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ValidationProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);

        Assert.Contains(
            nameof(AdjustInventoryStockRequest.QuantityDelta),
            problem.Errors.Keys);
    }

    [Fact]
    public async Task
        AdjustInventoryStock_BelowReservedQuantity_ReturnsBadRequestWithoutMutation()
    {
        InventoryItemResponse created =
            await CreateInventoryItemAsync(
                initialOnHandQuantity: 10);

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
                            candidate.Id == created.Id);

            Assert.True(
                item.TryReserve(
                    quantity: 6,
                    DateTimeOffset.UtcNow));

            await dbContext.SaveChangesAsync();
        }

        AdjustInventoryStockRequest requestBody = new()
        {
            QuantityDelta = -5
        };

        using HttpResponseMessage response =
            await SendAuthenticatedJsonAsync(
                HttpMethod.Post,
                $"{InventoryPath}/{created.Id}/stock-adjustments",
                requestBody,
                EshopRoles.Support);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetails? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>();

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
                        candidate.Id == created.Id);

        Assert.Equal(10, persisted.OnHandQuantity);
        Assert.Equal(6, persisted.ReservedQuantity);
        Assert.Equal(4, persisted.AvailableQuantity);
    }

    [Fact]
    public async Task
        MissingInventoryItem_OperationsReturnNotFound()
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
            QuantityDelta = 1
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
        InventoryRowVersion_ConcurrentUpdatesRejectStaleWrite()
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
                    item => item.Id == created.Id);

        InventoryItem secondEntity =
            await secondContext.InventoryItems
                .SingleAsync(
                    item => item.Id == created.Id);

        firstEntity.AdjustOnHandQuantity(
            quantityDelta: 1,
            DateTimeOffset.UtcNow);

        secondEntity.AdjustOnHandQuantity(
            quantityDelta: 1,
            DateTimeOffset.UtcNow);

        await firstContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());

        await using AsyncServiceScope verificationScope =
            fixture.Factory.Services.CreateAsyncScope();

        InventoryDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

        InventoryItem persisted =
            await verificationContext.InventoryItems
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == created.Id);

        Assert.Equal(11, persisted.OnHandQuantity);
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
                .ReadFromJsonAsync<InventoryItemResponse>();

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

        return await fixture.Client.SendAsync(request);
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

        return await fixture.Client.SendAsync(request);
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
