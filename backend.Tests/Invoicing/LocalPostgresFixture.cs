using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Tests.Invoicing;

/// <summary>
/// Shared local PostgreSQL database for the few tests that need a real database.
/// Uses the developer Postgres (README: localhost:5432, postgres/secret) or the host's Postgres via
/// host.docker.internal (sandbox), and creates one database per test run, so the tests never touch the
/// real data. On machines without any reachable database the fixture is marked unavailable and the
/// database-dependent tests fail with a clear message.
/// </summary>
public sealed class LocalPostgresFixture : IAsyncLifetime
{
    private const string DefaultServer = "Host=localhost;Port=5432;Username=postgres;Password=secret";

    private NpgsqlConnection? _connection;
    private string? _databaseName;
    private string _server = DefaultServer;

    public string ConnectionString { get; private set; } = "";
    public string Server { get => _server; }
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        // In sandboxes localhost is the sandbox itself; the developer Postgres runs on the host.
        foreach (var server in new[]
        {
            DefaultServer,
            "Host=host.docker.internal;Port=5432;Username=postgres;Password=secret",
        })
        {
            _server = server;
            _databaseName = $"scw_backoffice_test_{Guid.NewGuid():N}";
            try
            {
                await using var admin = new NpgsqlConnection($"{_server};Database=postgres");
                await admin.OpenAsync();
                await using var cmd = admin.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
                await cmd.ExecuteNonQueryAsync();
                Available = true;
                break;
            }
            catch (NpgsqlException)
            {
                _databaseName = null;
            }
        }

        if (!Available)
            return;

        ConnectionString = $"{_server};Database={_databaseName}";
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        // Apply the production migrations to the fresh database, exactly like Program.cs does at startup.
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .MigrationsAssembly(typeof(AppDbContext).Assembly);
        var db = new AppDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync();
        await db.DisposeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        if (_databaseName is not null)
        {
            await using var admin = new NpgsqlConnection($"{_server};Database=postgres");
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
