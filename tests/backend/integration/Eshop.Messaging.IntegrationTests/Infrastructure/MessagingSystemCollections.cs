using Xunit;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class MessagingTestCollections
{
    public const string System =
        "Messaging system integration tests";
}

[CollectionDefinition(
    MessagingTestCollections.System)]
public sealed class MessagingSystemCollectionDefinition
    : ICollectionFixture<MessagingSystemFixture>
{
}
