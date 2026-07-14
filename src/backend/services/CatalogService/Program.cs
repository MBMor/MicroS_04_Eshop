WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
