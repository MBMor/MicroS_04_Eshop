using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NotificationsService.Data;

public sealed class NotificationsDbContextFactory
    : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
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
            configuration.GetConnectionString("NotificationsDb")
            ?? throw new InvalidOperationException(
                "Connection string 'NotificationsDb' was not found.");

        DbContextOptionsBuilder<NotificationsDbContext>
            optionsBuilder = new();

        optionsBuilder.UseNpgsql(connectionString);

        return new NotificationsDbContext(
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
            "NotificationsService");

        return Directory.Exists(projectDirectory)
            ? projectDirectory
            : currentDirectory;
    }
}
