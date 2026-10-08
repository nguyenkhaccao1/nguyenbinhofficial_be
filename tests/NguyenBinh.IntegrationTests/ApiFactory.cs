using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace NguyenBinh.IntegrationTests;

/// <summary>
/// Chay API that tren mot database LocalDB tam (tao bang migration, xoa khi xong) —
/// khong bao gio cham vao database dev/production.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin-test-123";

    private readonly string _databaseName = $"NguyenBinhOfficial_Test_{Guid.NewGuid():N}"[..40];
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "nb-tests", Guid.NewGuid().ToString("N"));

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public ApiFactory()
    {
        // Program doc cau hinh ngay khi build service, nen dung bien moi truong (co hieu luc truoc CreateBuilder).
        var settings = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["ConnectionStrings__Default"] = ConnectionString(_databaseName),
            ["Database__MigrateOnStartup"] = "true",
            ["Seed__AdminEmail"] = AdminEmail,
            ["Seed__AdminPassword"] = AdminPassword,
            ["Jwt__Secret"] = "integration-test-secret-key-that-is-long-enough-123456",
            ["Auth__RefreshCookie__Secure"] = "false",
            ["Serilog__SqlSink__Enabled"] = "false",
            ["Serilog__MinimumLevel__Default"] = "Warning",
            ["Storage__RootPath"] = _storageRoot,
            ["RateLimits__AuthPerMinute"] = "1000",
            ["RateLimits__FormsPerMinute"] = "1000",
            ["Swagger__Enabled"] = "false",
        };
        foreach (var (key, value) in settings) Environment.SetEnvironmentVariable(key, value);
    }

    private static string ConnectionString(string database) =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>Email gui ra trong test (khong goi SMTP that).</summary>
    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Giong Development: phat hien som service khong dung duoc (thieu dang ky, sai lifetime).
        builder.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });
        builder.ConfigureTestServices(services =>
            services.AddSingleton<NguyenBinh.Application.Leads.IEmailSender>(Emails));
    }

    public Task InitializeAsync()
    {
        _ = Server; // khoi dong host → migrate + seed
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(ConnectionString("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID('{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END";
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
    }

    /// <summary>Client co cookie jar (giu refresh cookie) + da gan Bearer token.</summary>
    public async Task<HttpClient> LoginAsync(string email = AdminEmail, string password = AdminPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.ReadAsync<LoginData>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);
        return client;
    }

    public sealed record LoginData(string AccessToken, DateTimeOffset ExpiresAt, UserData User);

    public sealed record UserData(Guid Id, string Email, string FullName, List<string> Roles, List<string> Permissions);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public sealed record Envelope<T>(bool Success, T? Data, string? Message, Dictionary<string, string[]>? Errors);

public sealed record Paged<T>(List<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public static class HttpExtensions
{
    public static async Task<Envelope<T>> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Envelope<T>>(ApiFactory.Json))!;
}
