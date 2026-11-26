using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Api.Dtos;

namespace TodoApp.Tests.Integration;

/// <summary>
/// Boots the real API (real pipeline, real EF migrations, real JWT) against a private in-memory SQLite database.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source=file:todo_{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;

    public ApiFactory()
    {
        // A shared-cache in-memory database lives only while at least one connection is open.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Security:BcryptWorkFactor", "4");
        builder.UseSetting("Trash:PurgeEnabled", "false");
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, PipeWriterCompatStartupFilter>());
    }

    /// <summary>Registers a fresh user and returns an HttpClient already carrying their bearer token.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> CreateUserClientAsync(string? email = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email ?? $"user-{Guid.NewGuid():N}@example.com",
            Password = "Passw0rd!",
            DisplayName = "Integration User"
        });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
