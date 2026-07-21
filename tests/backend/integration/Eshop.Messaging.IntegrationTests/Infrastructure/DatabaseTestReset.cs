using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NotificationsService.Data;
using OrdersService.Data;
using PaymentsService.Data;

namespace Eshop.Messaging.IntegrationTests.Infrastructure;

public static class DatabaseTestReset
{
    public static async Task ResetAsync(
        MessagingSystemFixture fixture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await Task.WhenAll(
            TruncateAsync<OrdersDbContext>(
                fixture.OrdersFactory.Services,
                cancellationToken),

            TruncateAsync<InventoryDbContext>(
                fixture.InventoryFactory.Services,
                cancellationToken),

            TruncateAsync<PaymentsDbContext>(
                fixture.PaymentsFactory.Services,
                cancellationToken),

            TruncateAsync<NotificationsDbContext>(
                fixture.NotificationsFactory.Services,
                cancellationToken));
    }

    private static async Task TruncateAsync<TContext>(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await DatabaseTestScope.ExecuteAsync<TContext>(
            serviceProvider,
            async (dbContext, token) =>
            {
                string[] tableNames =
                    GetMappedTableNames(dbContext);

                if (tableNames.Length == 0)
                {
                    return;
                }

                string sql =
                    $"TRUNCATE TABLE " +
                    $"{string.Join(", ", tableNames)} " +
                    $"RESTART IDENTITY CASCADE;";

                await dbContext.Database
                    .ExecuteSqlRawAsync(
                        sql,
                        token);
            },
            cancellationToken);
    }

    private static string[] GetMappedTableNames(
        DbContext dbContext)
    {
        HashSet<string> tableNames =
            new(StringComparer.Ordinal);

        foreach (
            IEntityType entityType
            in dbContext.Model.GetEntityTypes())
        {
            string? tableName =
                entityType.GetTableName();

            if (tableName is null)
            {
                continue;
            }

            string? schema =
                entityType.GetSchema();

            string qualifiedTableName =
                schema is null
                    ? QuoteIdentifier(tableName)
                    : $"{QuoteIdentifier(schema)}." +
                      $"{QuoteIdentifier(tableName)}";

            tableNames.Add(
                qualifiedTableName);
        }

        return tableNames
            .OrderBy(
                tableName => tableName,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static string QuoteIdentifier(
        string identifier)
    {
        return $"\"{identifier.Replace(
            "\"",
            "\"\"",
            StringComparison.Ordinal)}\"";
    }
}
