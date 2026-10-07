using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NguyenBinh.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ContentTests(ApiFactory factory)
{
    private sealed record Meta(Guid Id, string Status, string RowVersion, bool IsPublic, bool IsDeleted);

    private sealed record Detail(Meta Meta, JsonObject Data);

    private sealed record ProjectRow(Guid Id, string Name, string Slug, string OwnershipType, string Status,
        List<string> ContentTypes, List<string> ProjectRoles);

    private sealed record VersionRow(Guid Id, int Version, bool IsAutosave, string? Note);

    private sealed record Bulk(int Succeeded, List<JsonObject> Failed);

    private sealed record Lookup(Guid Id, string Name, string? Extra);

    [Fact]
    public async Task Seed_contains_the_nine_projects_as_drafts_with_correct_ownership()
    {
        var admin = await factory.LoginAsync();
        var page = (await (await admin.GetAsync("/api/v1/admin/projects?pageSize=50")).ReadAsync<Paged<ProjectRow>>()).Data!;
        var seeded = page.Items.Where(p => !p.Name.Contains("bản sao") && !p.Slug.StartsWith("test-")).ToList();

        seeded.Select(p => p.Slug).Should().Contain(["nam-viet-hung", "perfectkey-web", "a-smart", "perfectkey-app",
            "perfectkey-workforce", "com-thi-no", "pos-nguyen-binh", "tuong-quang-phat", "quan-ly-khach-san"]);

        var seededNine = seeded.Where(p => p.Slug is "nam-viet-hung" or "perfectkey-web" or "a-smart" or "perfectkey-app"
            or "perfectkey-workforce" or "com-thi-no" or "pos-nguyen-binh" or "tuong-quang-phat" or "quan-ly-khach-san").ToList();
        seededNine.Should().HaveCount(9).And.OnlyContain(p => p.Status == "DRAFT" || p.Status == "PUBLISHED");

        seededNine.Single(p => p.Slug == "pos-nguyen-binh").OwnershipType.Should().Be("NGUYEN_BINH_OWNED");
        seededNine.Single(p => p.Slug == "pos-nguyen-binh").ContentTypes.Should().Contain("OWN_PRODUCT");
        seededNine.Single(p => p.Slug == "com-thi-no").OwnershipType.Should().Be("UNDISCLOSED");
        seededNine.Where(p => p.Slug is not ("pos-nguyen-binh" or "com-thi-no"))
            .Should().OnlyContain(p => p.OwnershipType == "CLIENT_OWNED" && !p.ContentTypes.Contains("OWN_PRODUCT"));
    }

    [Fact]
    public async Task Undisclosed_ownership_blocks_publishing_until_configured()
    {
        var admin = await factory.LoginAsync();
        var created = await CreateProjectAsync(admin, "test-undisclosed", ownership: "UNDISCLOSED");

        var blocked = await admin.PostAsync($"/api/v1/admin/projects/{created.Meta.Id}/publish", null);
        blocked.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await blocked.ReadAsync<object>()).Errors.Should().ContainKeys("ownershipType", "publicCreditText");

        var data = created.Data;
        data["ownershipType"] = "CLIENT_OWNED";
        data["publicCreditText"] = "Nguyên Bình tham gia phát triển hệ thống theo yêu cầu.";
        var updated = await admin.PutAsJsonAsync($"/api/v1/admin/projects/{created.Meta.Id}", data);
        updated.EnsureSuccessStatusCode();

        var published = await admin.PostAsync($"/api/v1/admin/projects/{created.Meta.Id}/publish", null);
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        (await published.ReadAsync<Detail>()).Data!.Meta.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Own_product_type_requires_nguyen_binh_ownership()
    {
        var admin = await factory.LoginAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/admin/projects", new
        {
            name = "Test own product", slug = "test-own-product", primaryContentType = "OWN_PRODUCT",
            ownershipType = "CLIENT_OWNED", projectRoles = new[] { "DEVELOPER" }, seo = new { },
        });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ReadAsync<object>()).Errors.Should().ContainKey("contentTypes");
    }

    [Fact]
    public async Task Stale_row_version_returns_409()
    {
        var admin = await factory.LoginAsync();
        var created = await CreateProjectAsync(admin, "test-concurrency");
        var stale = created.Meta.RowVersion;

        (await PutAsync(admin, created.Meta.Id, created.Data, stale)).EnsureSuccessStatusCode();
        var conflict = await PutAsync(admin, created.Meta.Id, created.Data, stale);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Duplicate_trash_restore_and_versions()
    {
        var admin = await factory.LoginAsync();
        var created = await CreateProjectAsync(admin, "test-lifecycle");
        var id = created.Meta.Id;

        var copy = (await (await admin.PostAsync($"/api/v1/admin/projects/{id}/duplicate", null)).ReadAsync<Detail>()).Data!;
        copy.Meta.Status.Should().Be("DRAFT");
        copy.Data["slug"]!.GetValue<string>().Should().NotBe("test-lifecycle").And.StartWith("test-lifecycle");

        // Sua → co them phien ban; khoi phuc phien ban dau tien.
        created.Data["shortDescription"] = "Mô tả đã sửa";
        (await PutAsync(admin, id, created.Data, null)).EnsureSuccessStatusCode();
        var versions = (await (await admin.GetAsync($"/api/v1/admin/projects/{id}/versions")).ReadAsync<List<VersionRow>>()).Data!;
        versions.Should().HaveCountGreaterThanOrEqualTo(2);
        var first = versions.OrderBy(v => v.Version).First();
        var restored = (await (await admin.PostAsync($"/api/v1/admin/projects/{id}/versions/{first.Id}/restore", null))
            .ReadAsync<Detail>()).Data!;
        restored.Data["shortDescription"]!.GetValue<string>().Should().Be("Mô tả ngắn");

        // Thung rac
        (await admin.DeleteAsync($"/api/v1/admin/projects/{id}")).EnsureSuccessStatusCode();
        var active = (await (await admin.GetAsync("/api/v1/admin/projects?q=test-lifecycle&pageSize=50")).ReadAsync<Paged<ProjectRow>>()).Data!;
        active.Items.Should().NotContain(p => p.Id == id);
        var trash = (await (await admin.GetAsync("/api/v1/admin/projects?trash=true&pageSize=100")).ReadAsync<Paged<ProjectRow>>()).Data!;
        trash.Items.Should().Contain(p => p.Id == id);

        (await admin.PostAsync($"/api/v1/admin/projects/{id}/restore", null)).EnsureSuccessStatusCode();
        (await admin.GetAsync($"/api/v1/admin/projects/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Autosave_does_not_change_the_saved_content()
    {
        var admin = await factory.LoginAsync();
        var created = await CreateProjectAsync(admin, "test-autosave");
        created.Data["shortDescription"] = "Bản nháp đang gõ";

        var autosave = await admin.PutAsJsonAsync($"/api/v1/admin/projects/{created.Meta.Id}/autosave", created.Data);
        autosave.EnsureSuccessStatusCode();
        (await autosave.ReadAsync<VersionRow>()).Data!.IsAutosave.Should().BeTrue();

        var current = (await (await admin.GetAsync($"/api/v1/admin/projects/{created.Meta.Id}")).ReadAsync<Detail>()).Data!;
        current.Data["shortDescription"]!.GetValue<string>().Should().Be("Mô tả ngắn");
    }

    [Fact]
    public async Task Schedule_requires_future_time_and_marks_scheduled()
    {
        var admin = await factory.LoginAsync();
        var created = await CreateProjectAsync(admin, "test-schedule", ownership: "CLIENT_OWNED");

        (await admin.PostAsJsonAsync($"/api/v1/admin/projects/{created.Meta.Id}/schedule",
            new { publishAt = DateTimeOffset.UtcNow.AddHours(-1) })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var scheduled = await admin.PostAsJsonAsync($"/api/v1/admin/projects/{created.Meta.Id}/schedule",
            new { publishAt = DateTimeOffset.UtcNow.AddDays(1) });
        scheduled.EnsureSuccessStatusCode();
        var meta = (await scheduled.ReadAsync<Detail>()).Data!.Meta;
        meta.Status.Should().Be("SCHEDULED");
        meta.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Bulk_publish_reports_items_that_fail_validation()
    {
        var admin = await factory.LoginAsync();
        var ok = await CreateProjectAsync(admin, "test-bulk-ok", ownership: "CLIENT_OWNED");
        var bad = await CreateProjectAsync(admin, "test-bulk-bad", ownership: "UNDISCLOSED");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/projects/bulk",
            new { action = "publish", ids = new[] { ok.Meta.Id, bad.Meta.Id } });
        response.EnsureSuccessStatusCode();
        var result = (await response.ReadAsync<Bulk>()).Data!;
        result.Succeeded.Should().Be(1);
        result.Failed.Should().ContainSingle(f => f["id"]!.GetValue<Guid>() == bad.Meta.Id);
    }

    [Fact]
    public async Task Blog_posts_and_categories_share_the_slug_namespace()
    {
        var admin = await factory.LoginAsync();
        var post = await admin.PostAsJsonAsync("/api/v1/admin/posts", new { title = "Công nghệ", slug = "cong-nghe", seo = new { } });
        post.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); // "cong-nghe" la danh muc da seed
        (await post.ReadAsync<object>()).Errors.Should().ContainKey("slug");
    }

    [Fact]
    public async Task Rich_text_is_sanitized()
    {
        var admin = await factory.LoginAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/admin/posts", new
        {
            title = "Test sanitize", slug = "test-sanitize",
            contentHtml = "<p onclick=\"alert(1)\">Xin chào</p><script>alert(1)</script><iframe src=\"https://evil.example\"></iframe>",
            seo = new { },
        });
        response.EnsureSuccessStatusCode();
        var html = (await response.ReadAsync<Detail>()).Data!.Data["contentHtml"]!.GetValue<string>();
        html.Should().Contain("Xin chào").And.NotContain("script").And.NotContain("onclick").And.NotContain("evil.example");
    }

    [Fact]
    public async Task Page_paths_are_normalized_and_reserved_paths_rejected()
    {
        var admin = await factory.LoginAsync();
        var landing = await admin.PostAsJsonAsync("/api/v1/admin/pages", new
        {
            title = "Phần mềm POS nhà hàng", path = "Phần Mềm POS Nhà Hàng/", pageType = "LANDING", seo = new { },
            sections = Array.Empty<object>(),
        });
        landing.EnsureSuccessStatusCode();
        (await landing.ReadAsync<Detail>()).Data!.Data["path"]!.GetValue<string>().Should().Be("/phan-mem-pos-nha-hang");

        var solution = await admin.PostAsJsonAsync("/api/v1/admin/pages", new
        {
            title = "Giải pháp bán lẻ test", path = "ban-le-test", pageType = "SOLUTION", seo = new { },
            sections = Array.Empty<object>(),
        });
        (await solution.ReadAsync<Detail>()).Data!.Data["path"]!.GetValue<string>().Should().Be("/giai-phap/ban-le-test");

        var reserved = await admin.PostAsJsonAsync("/api/v1/admin/pages", new
        {
            title = "Không hợp lệ", path = "/admin/x", pageType = "STANDARD", seo = new { }, sections = Array.Empty<object>(),
        });
        reserved.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Unknown_block_type_is_rejected_and_custom_html_needs_permission()
    {
        var admin = await factory.LoginAsync();
        var unknown = await admin.PostAsJsonAsync("/api/v1/admin/pages", new
        {
            title = "Block lạ", path = "/test-block-la", pageType = "STANDARD", seo = new { },
            sections = new[] { new { name = "S1", blocks = new[] { new { type = "MARQUEE", data = new { } } } } },
        });
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "editor.html@test.local", fullName = "Editor", password = "Editor-12345", roles = new[] { "Editor" },
        })).EnsureSuccessStatusCode();
        var editor = await factory.LoginAsync("editor.html@test.local", "Editor-12345");

        var html = await editor.PostAsJsonAsync("/api/v1/admin/pages", new
        {
            title = "HTML", path = "/test-html", pageType = "STANDARD", seo = new { },
            sections = new[] { new { name = "S1", blocks = new[] { new { type = "CUSTOM_HTML", data = new { html = "<div>x</div>" } } } } },
        });
        html.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Role_permissions_apply_to_content_actions()
    {
        var admin = await factory.LoginAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "seo.content@test.local", fullName = "SEO", password = "SeoRole-12345", roles = new[] { "SEO" },
        })).EnsureSuccessStatusCode();
        var seo = await factory.LoginAsync("seo.content@test.local", "SeoRole-12345");
        var project = await CreateProjectAsync(admin, "test-seo-role", ownership: "CLIENT_OWNED");

        (await seo.GetAsync("/api/v1/admin/projects")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PutAsync(seo, project.Meta.Id, project.Data, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await seo.PostAsync($"/api/v1/admin/projects/{project.Meta.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await seo.DeleteAsync($"/api/v1/admin/projects/{project.Meta.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await seo.PostAsJsonAsync("/api/v1/admin/projects/bulk", new { action = "delete", ids = new[] { project.Meta.Id } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Menus_are_seeded_and_saved_as_a_two_level_tree()
    {
        var admin = await factory.LoginAsync();
        var header = (await (await admin.GetAsync("/api/v1/admin/menus/header")).ReadAsync<JsonObject>()).Data!;
        header["items"]!.AsArray().Select(i => i!["label"]!.GetValue<string>())
            .Should().ContainInOrder("Sản phẩm", "Giải pháp", "Dịch vụ", "Dự án", "Công nghệ", "Blog", "Giới thiệu");

        var tooDeep = await admin.PutAsJsonAsync("/api/v1/admin/menus/footer-legal", new
        {
            name = "Pháp lý",
            items = new[] { new { label = "A", url = "/a", children = new[] { new { label = "B", url = "/b", children = new[] { new { label = "C", url = "/c" } } } } } },
        });
        tooDeep.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Lookups_and_seeded_home_page()
    {
        var admin = await factory.LoginAsync();
        var lookups = (await (await admin.GetAsync("/api/v1/admin/lookups")).ReadAsync<Dictionary<string, List<Lookup>>>()).Data!;
        lookups["industries"].Select(i => i.Name).Should().Contain(["Nhà hàng", "Khách sạn", "Nhân sự"]);
        lookups["technologies"].Select(i => i.Name).Should().Contain([".NET", "React", "Flutter", "SQL Server"]);
        lookups["products"].Should().Contain(p => p.Extra == "pos-nguyen-binh");

        var pages = (await (await admin.GetAsync("/api/v1/admin/pages?pageType=HOME")).ReadAsync<Paged<JsonObject>>()).Data!;
        var home = pages.Items.Single();
        home["path"]!.GetValue<string>().Should().Be("/");
        home["sectionCount"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(12);
    }

    private static async Task<Detail> CreateProjectAsync(HttpClient client, string slug, string ownership = "UNDISCLOSED")
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/projects", new
        {
            name = $"Dự án {slug}", slug, shortDescription = "Mô tả ngắn", primaryContentType = "CUSTOM_PROJECT",
            ownershipType = ownership, projectRoles = new[] { "DEVELOPER" },
            publicCreditText = ownership == "UNDISCLOSED" ? null : "Nguyên Bình tham gia phát triển theo yêu cầu.",
            features = new[] { new { title = "Quản lý đặt phòng" } },
            seo = new { },
        });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        return (await response.ReadAsync<Detail>()).Data!;
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, JsonObject data, string? rowVersion)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/projects/{id}")
        {
            Content = JsonContent.Create(data),
        };
        if (rowVersion is not null) request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        return client.SendAsync(request);
    }
}
