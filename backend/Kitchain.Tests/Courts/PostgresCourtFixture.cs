using Kitchain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Kitchain.Tests.Courts;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KITCHAIN_TEST_CONNECTION_STRING")))
            Skip = "Set KITCHAIN_TEST_CONNECTION_STRING to a local PostgreSQL database to run isolated-schema integration tests.";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KITCHAIN_TEST_CONNECTION_STRING")))
            Skip = "Set KITCHAIN_TEST_CONNECTION_STRING to a local PostgreSQL database to run isolated-schema integration tests.";
    }
}

public sealed class PostgresCourtFixture : IAsyncLifetime
{
    private readonly string _schema = $"kitchain_test_{Guid.NewGuid():N}";
    private string? _baseConnection;
    private bool _schemaCreated;
    private WebApplicationFactory<Program>? _factory;
    public string ConnectionString { get; private set; } = string.Empty;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _baseConnection = Environment.GetEnvironmentVariable("KITCHAIN_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(_baseConnection)) return;
        // The generated identifier contains only a fixed prefix and hexadecimal GUID.
        await ExecuteAsync($"CREATE SCHEMA \"{_schema}\"");
        _schemaCreated = true;
        ConnectionString = new NpgsqlConnectionStringBuilder(_baseConnection)
        {
            SearchPath = _schema,
            Pooling = false
        }.ConnectionString;
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentCourtSeeder.SeedAsync(db);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Kitchain"] = ConnectionString
                }));
        });
        Client = _factory.CreateClient();
    }

    public KitchainDbContext CreateDbContext() => new(new DbContextOptionsBuilder<KitchainDbContext>()
        .UseNpgsql(ConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", _schema)).Options);

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_schemaCreated) await ExecuteAsync($"DROP SCHEMA \"{_schema}\" CASCADE");
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_baseConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
