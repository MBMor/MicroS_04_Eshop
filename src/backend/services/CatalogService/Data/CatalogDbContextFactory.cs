using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CatalogService.Data;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveBasePath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("Connection string 'CatalogDb' was not found.");

        DbContextOptionsBuilder<CatalogDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);

        return new CatalogDbContext(optionsBuilder.Options);
    }

    private static string ResolveBasePath()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string projectDirectory = Path.Combine(currentDirectory, "src", "backend", "services", "CatalogService");

        return Directory.Exists(projectDirectory)
            ? projectDirectory
            : currentDirectory;
    }
}
