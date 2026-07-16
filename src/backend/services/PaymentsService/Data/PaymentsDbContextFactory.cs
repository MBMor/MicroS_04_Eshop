using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentsService.Data;

public sealed class PaymentsDbContextFactory
    : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
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
            configuration.GetConnectionString("PaymentsDb")
            ?? throw new InvalidOperationException(
                "Connection string 'PaymentsDb' was not found.");

        DbContextOptionsBuilder<PaymentsDbContext>
            optionsBuilder = new();

        optionsBuilder.UseNpgsql(connectionString);

        return new PaymentsDbContext(
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
            "PaymentsService");

        return Directory.Exists(projectDirectory)
            ? projectDirectory
            : currentDirectory;
    }
}
