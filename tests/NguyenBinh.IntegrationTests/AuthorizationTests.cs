using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NguyenBinh.Api.Authorization;

namespace NguyenBinh.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_api_without_token_is_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadAsync<object>()).Success.Should().BeFalse();
    }

    [Fact]
    public async Task Viewer_can_read_but_not_write_or_manage_users()
    {
        var admin = await factory.LoginAsync();
        const string email = "viewer.authz@test.local";
        (await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email, fullName = "Viewer", password = "Viewer-12345", roles = new[] { "Viewer" },
        })).EnsureSuccessStatusCode();

        var viewer = await factory.LoginAsync(email, "Viewer-12345");
        (await viewer.GetAsync("/api/v1/admin/settings")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PutAsJsonAsync("/api/v1/admin/settings/contact", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync("/api/v1/admin/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Role_permission_change_applies_on_next_request()
    {
        var admin = await factory.LoginAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/roles",
            new { name = "Tester", description = "test", permissions = new[] { "dashboard.view" } }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "tester@test.local", fullName = "Tester", password = "Tester-12345", roles = new[] { "Tester" },
        })).EnsureSuccessStatusCode();

        var tester = await factory.LoginAsync("tester@test.local", "Tester-12345");
        (await tester.GetAsync("/api/v1/admin/media")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var roles = await (await admin.GetAsync("/api/v1/admin/roles")).ReadAsync<List<RoleData>>();
        var roleId = roles.Data!.Single(r => r.Name == "Tester").Id;
        (await admin.PutAsJsonAsync($"/api/v1/admin/roles/{roleId}",
            new { name = "Tester", description = "test", permissions = new[] { "dashboard.view", "media.view" } }))
            .EnsureSuccessStatusCode();

        // Cung access token cu, quyen moi co hieu luc ngay.
        (await tester.GetAsync("/api/v1/admin/media")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_cannot_grant_permissions_they_do_not_have()
    {
        var superAdmin = await factory.LoginAsync();
        (await superAdmin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "admin2@test.local", fullName = "Admin 2", password = "Admin2-12345", roles = new[] { "Admin" },
        })).EnsureSuccessStatusCode();

        var admin = await factory.LoginAsync("admin2@test.local", "Admin2-12345");
        var response = await admin.PostAsJsonAsync("/api/v1/admin/roles",
            new { name = "Purger", description = "x", permissions = new[] { "system.purge" } });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var assignSuper = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "sneaky@test.local", fullName = "Sneaky", password = "Sneaky-12345", roles = new[] { "SuperAdmin" },
        });
        assignSuper.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Last_super_admin_cannot_be_demoted()
    {
        var admin = await factory.LoginAsync();
        var users = await (await admin.GetAsync("/api/v1/admin/users?role=SuperAdmin")).ReadAsync<Paged<UserRow>>();
        var self = users.Data!.Items.Single(u => u.Email == ApiFactory.AdminEmail);

        var response = await admin.DeleteAsync($"/api/v1/admin/users/{self.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Moi endpoint admin (tru auth) phai khai bao permission — chan viec quen gan quyen.</summary>
    [Fact]
    public void Every_admin_endpoint_declares_a_permission()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("api/v1/admin", StringComparison.OrdinalIgnoreCase) == true)
            .Where(e => !e.RoutePattern.RawText!.StartsWith("api/v1/admin/auth", StringComparison.OrdinalIgnoreCase))
            .ToList();

        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
        {
            e.Metadata.GetOrderedMetadata<HasPermissionAttribute>().Should()
                .NotBeEmpty($"{e.DisplayName} must have [HasPermission]");
            e.Metadata.GetMetadata<IAllowAnonymous>().Should().BeNull($"{e.DisplayName} must not allow anonymous");
        });
    }

    private sealed record RoleData(Guid Id, string Name, List<string> Permissions);

    private sealed record UserRow(Guid Id, string Email, List<string> Roles);
}
