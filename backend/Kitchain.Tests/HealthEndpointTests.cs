using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Kitchain.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Liveness_is_healthy_without_database_configuration()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_is_unhealthy_without_database_configuration()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApi_is_available_only_in_development(string environment, HttpStatusCode expected)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Discovery_database_failure_returns_safe_problem_details()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/courts");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stackTrace", body);
        Assert.DoesNotContain("connection", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Kitchain"] = null
                }));
        });
}
