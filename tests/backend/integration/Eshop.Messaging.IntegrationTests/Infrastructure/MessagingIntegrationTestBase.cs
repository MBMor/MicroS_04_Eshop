using Xunit;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public abstract class MessagingIntegrationTestBase(
    MessagingSystemFixture fixture)
    : IAsyncLifetime
{
    protected MessagingSystemFixture Fixture { get; } =
        fixture;

    public Task InitializeAsync()
    {
        return Fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
