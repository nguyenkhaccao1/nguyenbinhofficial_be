using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace NguyenBinh.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory factory)
{
    private const string RefreshUrl = "/api/v1/admin/auth/refresh";

    [Fact]
    public async Task Login_returns_token_user_and_httponly_refresh_cookie()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadAsync<ApiFactory.LoginData>();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.User.Roles.Should().Contain("SuperAdmin");

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("nb_rt="));
        cookie.Should().Contain("httponly").And.Contain("samesite=strict").And.Contain("path=/api/v1/admin/auth");
        body.Data.AccessToken.Should().NotBe(cookie, "refresh token must never be returned in the body");
    }

    [Fact]
    public async Task Wrong_password_is_401_with_generic_message()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login",
            new { email = ApiFactory.AdminEmail, password = "wrong-password-1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadAsync<object>()).Message.Should().Be("Email hoặc mật khẩu không đúng.");
    }

    [Fact]
    public async Task Invalid_login_payload_is_422_with_field_errors()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login",
            new { email = "not-an-email", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.ReadAsync<object>();
        body.Errors.Should().ContainKeys("email", "password");
    }

    [Fact]
    public async Task Refresh_requires_anti_forgery_header()
    {
        var client = await factory.LoginAsync();
        var response = await client.PostAsync(RefreshUrl, null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_reuse_of_old_token_revokes_the_family()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        var first = RefreshCookie(login);

        var rotated = await Refresh(client, first);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = RefreshCookie(rotated);
        second.Should().NotBe(first);

        // Dung lai token cu (gia lap token bi danh cap) → 401 va ca family bi thu hoi.
        (await Refresh(client, first)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(client, second)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        var token = RefreshCookie(login);

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/logout");
        logout.Headers.Add("Cookie", $"nb_rt={token}");
        logout.Headers.Add("X-Requested-With", "nb-admin");
        (await client.SendAsync(logout)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Refresh(client, token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        var admin = await factory.LoginAsync();
        const string email = "lockout@test.local";
        (await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email, fullName = "Lockout", password = "Lockout-12345", roles = new[] { "Viewer" },
        })).EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { email, password = "wrong-password-1" });

        var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { email, password = "Lockout-12345" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadAsync<object>()).Message.Should().Contain("tạm khoá");
    }

    [Fact]
    public async Task Me_returns_permissions()
    {
        var client = await factory.LoginAsync();
        var me = await (await client.GetAsync("/api/v1/admin/auth/me")).ReadAsync<ApiFactory.UserData>();
        me.Data!.Permissions.Should().Contain("project.create").And.Contain("system.purge");
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl);
        request.Headers.Add("Cookie", $"nb_rt={token}");
        request.Headers.Add("X-Requested-With", "nb-admin");
        return client.SendAsync(request);
    }

    private static string RefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("nb_rt="))
            .Split(';')[0]["nb_rt=".Length..];
}
