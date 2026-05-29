using System.Net.Sockets;
using Xunit;

namespace OpportunityOS.IntegrationTests;

/// <summary>
/// Shared test PostgreSQL configuration. Override with the
/// POSTGRES_TEST_CONNECTION env var (e.g. in CI / docker compose).
/// </summary>
public static class TestDb
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=opportunity_os_tests;Username=postgres;Password=postgres";

    public static bool IsReachable()
    {
        // Cheap TCP probe so the suite skips (not fails) when no DB is running.
        var (host, port) = ParseHostPort(ConnectionString);
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(host, port).Wait(TimeSpan.FromMilliseconds(750))
                   && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static (string Host, int Port) ParseHostPort(string connectionString)
    {
        string host = "localhost";
        int port = 5432;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = kv[0].Trim().ToLowerInvariant();
            if (key == "host") host = kv[1].Trim();
            else if (key == "port" && int.TryParse(kv[1].Trim(), out var p)) port = p;
        }
        return (host, port);
    }
}

/// <summary>A Fact that auto-skips when no test PostgreSQL is reachable.</summary>
public sealed class DbFactAttribute : FactAttribute
{
    public DbFactAttribute()
    {
        if (!TestDb.IsReachable())
            Skip = "No PostgreSQL reachable at the test connection string; integration test skipped.";
    }
}
