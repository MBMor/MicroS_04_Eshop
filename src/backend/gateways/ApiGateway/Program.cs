WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

WebApplication app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "ApiGateway",
    Status = "Running"
}));

app.MapHealthChecks("/health");

app.MapReverseProxy();

app.Run();
