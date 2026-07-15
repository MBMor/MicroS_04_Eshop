using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryService.Data;

public sealed class InventoryDbContextFactory
    : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration =
            new ConfigurationBuilder()
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
            configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException(
                "Connection string 'InventoryDb' was not found.");

        DbContextOptionsBuilder<InventoryDbContext>
            optionsBuilder = new();

        optionsBuilder.UseNpgsql(connectionString);

        return new InventoryDbContext(
            optionsBuilder.Options);
    }

    private static string ResolveBasePath()
    {
        string currentDirectory =
            Directory.GetCurrentDirectory();

        string projectDirectory = Path.Combine(
            currentDirectory,
            "src",
            "backend",
            "services",
            "InventoryService");

        return Directory.Exists(projectDirectory)
            ? projectDirectory
            : currentDirectory;
    }
}
