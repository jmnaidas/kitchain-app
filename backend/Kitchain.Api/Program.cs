using Kitchain.Api.Health;
using Kitchain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Kitchain.Application.Courts;
using Kitchain.Infrastructure.Courts;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddScoped<CourtDiscoveryService>();
builder.Services.AddScoped<ICourtDiscoveryReader, EfCourtDiscoveryReader>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.DescribeAllParametersInCamelCase());
builder.Services.AddDbContext<KitchainDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Kitchain")));
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

if (args.Contains("--seed-courts", StringComparer.Ordinal))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Court sample seeding is permitted only in Development.");
    await using var scope = app.Services.CreateAsyncScope();
    var added = await DevelopmentCourtSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<KitchainDbContext>());
    app.Logger.LogInformation("Added {Count} fictional development venues. No existing records were changed.", added);
    return;
}

app.UseExceptionHandler();
app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness is independent of PostgreSQL availability.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();

// Allows the integration test host to discover the composition root.
public partial class Program { }
