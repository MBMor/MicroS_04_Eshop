using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BasketService.Contracts;
using BasketService.Data;
using BasketService.Domain;
using BasketService.Integration;
using BasketService.IntegrationTests.Infrastructure;
using Eshop.Security.Authorization;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace BasketService.IntegrationTests;

public sealed class BasketServiceIntegrationTests(
    BasketServiceFixture fixture)
    : IClassFixture<BasketServiceFixture>
{
    [Fact]
    public async Task
        HealthAnonymousRequestReturnsOk()
    {
        string[] endpoints =
        [
            "/live",
            "/ready",
            "/health"
        ];

        foreach (string endpoint in endpoints)
        {
            await AssertEndpointStatusAsync(
                endpoint,
                HttpStatusCode.OK,
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task
        ReadinessTracksRedisOutageAndRecoveryWhileLivenessStaysHealthy()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await AssertEndpointStatusAsync(
            "/ready",
            HttpStatusCode.OK,
            cancellationToken);

        await fixture.PauseRedisAsync(
            cancellationToken);

        try
        {
            await AssertEndpointStatusAsync(
                "/live",
                HttpStatusCode.OK,
                cancellationToken);

            using HttpResponseMessage unavailableResponse =
                await WaitForStatusAsync(
                    "/ready",
                    HttpStatusCode.ServiceUnavailable,
                    cancellationToken);

            string responseBody =
                await unavailableResponse.Content.ReadAsStringAsync(
                    cancellationToken);

            using JsonDocument unavailableHealth =
                JsonDocument.Parse(
                    responseBody);

            Assert.Equal(
                "Unhealthy",
                unavailableHealth.RootElement
                    .GetProperty("status")
                    .GetString());

            JsonElement unavailableRedis =
                Assert.Single(
                    unavailableHealth.RootElement
                        .GetProperty("checks")
                        .EnumerateArray()
                        .ToArray());

            Assert.Equal(
                "redis",
                unavailableRedis
                    .GetProperty("name")
                    .GetString());

            Assert.Equal(
                "Unhealthy",
                unavailableRedis
                    .GetProperty("status")
                    .GetString());

            Assert.DoesNotContain(
                fixture.RedisConnectionString,
                responseBody,
                StringComparison.OrdinalIgnoreCase);

            await AssertEndpointStatusAsync(
                "/health",
                HttpStatusCode.ServiceUnavailable,
                cancellationToken);
        }
        finally
        {
            await fixture.UnpauseRedisAsync(
                cancellationToken);
        }

        using HttpResponseMessage recoveredResponse =
            await WaitForStatusAsync(
                "/ready",
                HttpStatusCode.OK,
                cancellationToken);

        string recoveredBody =
            await recoveredResponse.Content
                .ReadAsStringAsync(
                    cancellationToken);

        using JsonDocument recoveredHealth =
            JsonDocument.Parse(
                recoveredBody);

        Assert.Equal(
            "Healthy",
            recoveredHealth.RootElement
                .GetProperty("status")
                .GetString());

        JsonElement recoveredRedis =
            Assert.Single(
                recoveredHealth.RootElement
                    .GetProperty("checks")
                    .EnumerateArray()
                    .ToArray());

        Assert.Equal(
            "redis",
            recoveredRedis
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            "Healthy",
            recoveredRedis
                .GetProperty("status")
                .GetString());
    }

    [Fact]
    public async Task
        BasketAnonymousRequestReturnsUnauthorized()
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(
                "/api/v1/basket",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task
        BasketSupportUserReturnsForbidden()
    {
        using HttpRequestMessage request =
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                CreateSubject("support"),
                EshopRoles.Support);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task
        GetBasketNewCustomerReturnsEmptyBasket()
    {
        string subject =
            CreateSubject("empty");

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                subject);

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        BasketResponse? basket =
            await response.Content
                .ReadFromJsonAsync<BasketResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(basket);
        Assert.Empty(basket.Items);
        Assert.Empty(basket.Totals);

        Assert.True(
            basket.ExpiresAtUtc >
            basket.UpdatedAtUtc);

        Assert.Equal(
            TimeSpan.FromMinutes(60),
            basket.ExpiresAtUtc -
            basket.UpdatedAtUtc);
    }

    [Fact]
    public async Task
        AddItemActiveProductPersistsInRedisAndIsolatesCustomers()
    {
        CatalogProductSnapshot product =
            fixture.CatalogClient.RegisterProduct();

        string firstCustomer =
            CreateSubject("alice");

        string secondCustomer =
            CreateSubject("bob");

        using HttpRequestMessage addRequest =
            CreateCustomerRequest(
                HttpMethod.Post,
                "/api/v1/basket/items",
                firstCustomer);

        addRequest.Content = JsonContent.Create(
            new
            {
                productId = product.Id,
                quantity = 2
            });

        using HttpResponseMessage addResponse =
            await fixture.Client.SendAsync(addRequest, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            addResponse.StatusCode);

        using HttpRequestMessage firstGetRequest =
            CreateCustomerRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                firstCustomer);

        using HttpResponseMessage firstGetResponse =
            await fixture.Client.SendAsync(
                firstGetRequest,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            firstGetResponse.StatusCode);

        using JsonDocument firstBasket =
            await ReadJsonAsync(firstGetResponse);

        JsonElement firstItem =
            Assert.Single(
                firstBasket.RootElement
                    .GetProperty("items")
                    .EnumerateArray()
                    .ToArray());

        Assert.Equal(
            product.Id,
            firstItem.GetProperty("productId").GetGuid());

        Assert.Equal(
            2,
            firstItem.GetProperty("quantity").GetInt32());

        using HttpRequestMessage secondGetRequest =
            CreateCustomerRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                secondCustomer);

        using HttpResponseMessage secondGetResponse =
            await fixture.Client.SendAsync(
                secondGetRequest,
                TestContext.Current.CancellationToken);

        using JsonDocument secondBasket =
            await ReadJsonAsync(secondGetResponse);

        Assert.Equal(
            0,
            secondBasket.RootElement
                .GetProperty("items")
                .GetArrayLength());
    }

    [Fact]
    public async Task
        AddItemUnknownProductReturnsNotFound()
    {
        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                "/api/v1/basket/items",
                CreateSubject("unknown-product"));

        request.Content = JsonContent.Create(
            new
            {
                productId = Guid.NewGuid(),
                quantity = 1
            });

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task
        AddItemInactiveProductReturnsBadRequest()
    {
        CatalogProductSnapshot product =
            fixture.CatalogClient.RegisterProduct(
                isActive: false);

        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                "/api/v1/basket/items",
                CreateSubject("inactive-product"));

        request.Content = JsonContent.Create(
            new
            {
                productId = product.Id,
                quantity = 1
            });

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task
        BasketMutationFlowUpdateRemoveAndClearPersistsChanges()
    {
        CatalogProductSnapshot firstProduct =
            fixture.CatalogClient.RegisterProduct(
                name: "Keyboard");

        CatalogProductSnapshot secondProduct =
            fixture.CatalogClient.RegisterProduct(
                name: "Mouse",
                priceAmount: 900m);

        string subject =
            CreateSubject("mutations");

        await AddItemAsync(
            subject,
            firstProduct.Id,
            quantity: 1);

        await AddItemAsync(
            subject,
            secondProduct.Id,
            quantity: 2);

        using HttpRequestMessage updateRequest =
            CreateCustomerRequest(
                HttpMethod.Put,
                $"/api/v1/basket/items/{firstProduct.Id}",
                subject);

        updateRequest.Content = JsonContent.Create(
            new
            {
                quantity = 4
            });

        using HttpResponseMessage updateResponse =
            await fixture.Client.SendAsync(updateRequest, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        using JsonDocument updatedBasket =
            await ReadJsonAsync(updateResponse);

        JsonElement updatedItem =
            updatedBasket.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Single(
                    item =>
                        item.GetProperty("productId")
                            .GetGuid() == firstProduct.Id);

        Assert.Equal(
            4,
            updatedItem.GetProperty("quantity").GetInt32());

        using HttpRequestMessage removeRequest =
            CreateCustomerRequest(
                HttpMethod.Delete,
                $"/api/v1/basket/items/{firstProduct.Id}",
                subject);

        using HttpResponseMessage removeResponse =
            await fixture.Client.SendAsync(removeRequest, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            removeResponse.StatusCode);

        using HttpRequestMessage getAfterRemoveRequest =
            CreateCustomerRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                subject);

        using HttpResponseMessage getAfterRemoveResponse =
            await fixture.Client.SendAsync(
                getAfterRemoveRequest,
                TestContext.Current.CancellationToken);

        using JsonDocument basketAfterRemove =
            await ReadJsonAsync(getAfterRemoveResponse);

        JsonElement remainingItem =
            Assert.Single(
                basketAfterRemove.RootElement
                    .GetProperty("items")
                    .EnumerateArray()
                    .ToArray());

        Assert.Equal(
            secondProduct.Id,
            remainingItem.GetProperty("productId").GetGuid());

        using HttpRequestMessage clearRequest =
            CreateCustomerRequest(
                HttpMethod.Delete,
                "/api/v1/basket",
                subject);

        using HttpResponseMessage clearResponse =
            await fixture.Client.SendAsync(clearRequest, TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            clearResponse.StatusCode);

        using HttpRequestMessage getAfterClearRequest =
            CreateCustomerRequest(
                HttpMethod.Get,
                "/api/v1/basket",
                subject);

        using HttpResponseMessage getAfterClearResponse =
            await fixture.Client.SendAsync(
                getAfterClearRequest,
                TestContext.Current.CancellationToken);

        using JsonDocument basketAfterClear =
            await ReadJsonAsync(getAfterClearResponse);

        Assert.Equal(
            0,
            basketAfterClear.RootElement
                .GetProperty("items")
                .GetArrayLength());
    }

    [Fact]
    public async Task
        RedisRepositorySetAndGetRoundTripsSerializedBasket()
    {
        string customerId =
            CreateSubject("repository-roundtrip");

        Guid productId =
            Guid.NewGuid();

        ShoppingBasket basket = new(
            customerId,
            [
                new BasketItem(
                    productId,
                    "Keyboard",
                    UnitPrice: 2_500m,
                    Currency: "CZK",
                    Quantity: 2)
            ],
            DateTimeOffset.UtcNow);

        await using (
            AsyncServiceScope writeScope =
                fixture.Factory.Services
                    .CreateAsyncScope())
        {
            IBasketRepository repository =
                writeScope.ServiceProvider
                    .GetRequiredService<IBasketRepository>();

            await repository.SetAsync(
                basket,
                CancellationToken.None);
        }

        await using AsyncServiceScope readScope =
            fixture.Factory.Services
                .CreateAsyncScope();

        IBasketRepository readRepository =
            readScope.ServiceProvider
                .GetRequiredService<IBasketRepository>();

        ShoppingBasket? persistedBasket =
            await readRepository.GetAsync(
                customerId,
                CancellationToken.None);

        Assert.NotNull(persistedBasket);
        Assert.Equal(customerId, persistedBasket.CustomerId);

        BasketItem item =
            Assert.Single(persistedBasket.Items);

        Assert.Equal(productId, item.ProductId);
        Assert.Equal(2, item.Quantity);

        await readRepository.DeleteAsync(
            customerId,
            CancellationToken.None);

        ShoppingBasket? deletedBasket =
            await readRepository.GetAsync(
                customerId,
                CancellationToken.None);

        Assert.Null(deletedBasket);
    }

    [Fact]
    public async Task
        RedisRepositorySetAssignsExpectedAbsoluteExpiration()
    {
        string customerId =
            CreateSubject("repository-ttl");

        ShoppingBasket basket = new(
            customerId,
            [],
            DateTimeOffset.UtcNow);

        await using (
            AsyncServiceScope scope =
                fixture.Factory.Services
                    .CreateAsyncScope())
        {
            IBasketRepository repository =
                scope.ServiceProvider
                    .GetRequiredService<IBasketRepository>();

            await repository.SetAsync(
                basket,
                CancellationToken.None);
        }

        await using ConnectionMultiplexer connection =
            await ConnectionMultiplexer.ConnectAsync(
                fixture.RedisConnectionString);

        IDatabase database =
            connection.GetDatabase();

        string redisKey =
            $"eshop:{BasketKeyFactory.Create(customerId)}";

        TimeSpan? ttl =
            await database.KeyTimeToLiveAsync(redisKey);

        Assert.NotNull(ttl);

        Assert.InRange(
            ttl.Value,
            TimeSpan.FromMinutes(55),
            TimeSpan.FromMinutes(60));
    }

    private async Task AddItemAsync(
        string subject,
        Guid productId,
        int quantity)
    {
        using HttpRequestMessage request =
            CreateCustomerRequest(
                HttpMethod.Post,
                "/api/v1/basket/items",
                subject);

        request.Content = JsonContent.Create(
            new
            {
                productId,
                quantity
            });

        using HttpResponseMessage response =
            await fixture.Client.SendAsync(request, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    private static HttpRequestMessage
        CreateCustomerRequest(
            HttpMethod method,
            string path,
            string subject)
    {
        return CreateAuthenticatedRequest(
            method,
            path,
            subject,
            EshopRoles.Customer);
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

    private static async Task<JsonDocument>
        ReadJsonAsync(
            HttpResponseMessage response)
    {
        string content =
            await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        return JsonDocument.Parse(content);
    }

    private static string CreateSubject(
        string scenario)
    {
        return $"{scenario}-{Guid.NewGuid():N}";
    }

    private async Task AssertEndpointStatusAsync(
        string endpoint,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await fixture.Client.GetAsync(
                endpoint,
                cancellationToken);

        Assert.Equal(
            expectedStatus,
            response.StatusCode);
    }

    private async Task<HttpResponseMessage> WaitForStatusAsync(
        string endpoint,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        while (true)
        {
            HttpResponseMessage response =
                await fixture.Client.GetAsync(
                    endpoint,
                    timeout.Token);

            if (response.StatusCode == expectedStatus)
            {
                return response;
            }

            response.Dispose();

            await Task.Delay(
                TimeSpan.FromMilliseconds(250),
                timeout.Token);
        }
    }
}
