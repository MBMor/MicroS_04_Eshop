using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrdersService.Data;

public sealed class OrdersDbContextFactory
    : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveBasePath())
            .AddJsonFile(
                "appsettings.json",
                optional: false)
            .AddJsonFile(
                "appsettings.Development.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString =
            configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException(
                "Connection string 'OrdersDb' was not found.");

        DbContextOptionsBuilder<OrdersDbContext> optionsBuilder = new();

        optionsBuilder.UseNpgsql(connectionString);

        return new OrdersDbContext(optionsBuilder.Options);
    }

    private static string ResolveBasePath()
    {
        string currentDirectory = Directory.GetCurrentDirectory();

        string projectDirectory = Path.Combine(
            currentDirectory,
            "src",
            "backend",
            "services",
            "OrdersService");

        return Directory.Exists(projectDirectory)
            ? projectDirectory
            : currentDirectory;
    }
}
