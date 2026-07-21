using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class DatabaseTestScope
{
    public static async Task<TResult> ExecuteAsync<
        TContext,
        TResult>(
        IServiceProvider serviceProvider,
        Func<TContext, CancellationToken, Task<TResult>>
            operation,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(
            serviceProvider);

        ArgumentNullException.ThrowIfNull(
            operation);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        TContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<TContext>();

        return await operation(
            dbContext,
            cancellationToken);
    }

    public static async Task ExecuteAsync<TContext>(
        IServiceProvider serviceProvider,
        Func<TContext, CancellationToken, Task>
            operation,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(
            serviceProvider);

        ArgumentNullException.ThrowIfNull(
            operation);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        TContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<TContext>();

        await operation(
            dbContext,
            cancellationToken);
    }
}
