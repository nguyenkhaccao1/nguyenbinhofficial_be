using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NguyenBinh.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PublicContentTests(ApiFactory factory)
{
    private sealed record Created(JsonObject Meta, JsonObject Data);

    [Fact]
    public async Task Drafts_are_not_public()
    {
        var anonymous = factory.CreateClient();
        // Seed: 9 du an deu la ban nhap.
        (await anonymous.GetAsync("/api/v1/projects/perfectkey-workforce")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/api/v1/products/pos-nguyen-binh")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/api/v1/pages/by-path?path=/")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Published_client_project_hides_everything_not_permitted()
    {
        var admin = await factory.LoginAsync();
        var clientId = await CreateAsync(admin, "clients", new { name = "Khách hàng bí mật", slug = "khach-hang-bi-mat" });
        var logo = await UploadAsync(admin);

        var project = await CreateAsync(admin, "projects", new
        {
            name = "Dự án công khai hạn chế", slug = "pub-restricted", shortDescription = "Hệ thống quản lý đặt phòng",
            ownershipType = "CLIENT_OWNED", projectRoles = new[] { "DEVELOPER", "BACKEND" },
            publicCreditText = "Nguyên Bình phát triển theo yêu cầu.", clientId, coverMediaId = logo,
            websiteUrl = "https://example.com", canShowClient = false, canShowScreenshots = false, canShowMetrics = false,
            canShowLiveUrl = false, canShowTechnology = true,
            features = new object[] { new { title = "Đặt phòng", isPublic = true }, new { title = "Chức năng nội bộ", isPublic = false } },
            metrics = new[] { new { label = "Khách sạn", value = "10" } },
            seo = new { },
        });
        (await admin.PostAsync($"/api/v1/admin/projects/{project}/publish", null)).EnsureSuccessStatusCode();

        var detail = await GetAsync(factory.CreateClient(), "/api/v1/projects/pub-restricted");
        detail["card"]!["isOwnProduct"]!.GetValue<bool>().Should().BeFalse();
        detail["card"]!["creditText"]!.GetValue<string>().Should().Be("Nguyên Bình phát triển theo yêu cầu.");
        detail["card"]!["projectRoles"]!.AsArray().Select(r => r!.GetValue<string>()).Should().Equal("DEVELOPER", "BACKEND");
        detail["client"].Should().BeNull("canShowClient = false");
        detail["cover"].Should().BeNull("canShowScreenshots = false");
        detail["card"]!["image"].Should().BeNull();
        detail["metrics"]!.AsArray().Should().BeEmpty("canShowMetrics = false");
        detail["links"]!.AsArray().Should().BeEmpty("canShowLiveUrl = false");
        detail["features"]!.AsArray().Select(f => f!["title"]!.GetValue<string>()).Should().Equal("Đặt phòng");
    }

    [Fact]
    public async Task Own_product_is_labeled_and_permitted_data_is_shown()
    {
        var admin = await factory.LoginAsync();
        var image = await UploadAsync(admin);
        var project = await CreateAsync(admin, "projects", new
        {
            name = "Sản phẩm riêng", slug = "pub-own", shortDescription = "Phần mềm của Nguyên Bình",
            primaryContentType = "OWN_PRODUCT", ownershipType = "NGUYEN_BINH_OWNED", projectRoles = new[] { "OWNER" },
            coverMediaId = image, canShowScreenshots = true, canShowLiveUrl = true, websiteUrl = "https://example.org",
            isFeatured = true, seo = new { },
        });
        (await admin.PostAsync($"/api/v1/admin/projects/{project}/publish", null)).EnsureSuccessStatusCode();

        var detail = await GetAsync(factory.CreateClient(), "/api/v1/projects/pub-own");
        detail["card"]!["isOwnProduct"]!.GetValue<bool>().Should().BeTrue();
        detail["card"]!["creditText"].Should().BeNull();
        detail["cover"]!["url"]!.GetValue<string>().Should().StartWith("/media/");
        detail["links"]!.AsArray().Should().ContainSingle(l => l!["url"]!.GetValue<string>() == "https://example.org");

        var list = await GetAsync(factory.CreateClient(), "/api/v1/projects?pageSize=48");
        list["items"]!.AsArray().Should().Contain(p => p!["slug"]!.GetValue<string>() == "pub-own");
    }

    [Fact]
    public async Task Published_page_resolves_dynamic_blocks_and_media()
    {
        var admin = await factory.LoginAsync();
        var image = await UploadAsync(admin);
        var product = await CreateAsync(admin, "products", new
        {
            name = "Sản phẩm trang", slug = "pub-page-product", tagline = "Tagline", shortDescription = "Mô tả", isFeatured = true, seo = new { },
        });
        (await admin.PostAsync($"/api/v1/admin/products/{product}/publish", null)).EnsureSuccessStatusCode();

        var page = await CreateAsync(admin, "pages", new
        {
            title = "Trang thử", path = "/trang-thu-public", pageType = "STANDARD", seo = new { },
            sections = new object[]
            {
                new
                {
                    name = "S1", settings = new { tone = "dark" },
                    blocks = new object[]
                    {
                        new { type = "HERO", data = new { title = "Xin chào" } },
                        new { type = "IMAGE", data = new { mediaId = image } },
                        new { type = "PRODUCTS", data = new { source = "featured", limit = 3 } },
                        new { type = "TEXT", isEnabled = false, data = new { text = "ẩn" } },
                    },
                },
                new { name = "Tắt", isEnabled = false, blocks = new object[] { new { type = "TEXT", data = new { text = "x" } } } },
            },
        });
        (await admin.PostAsync($"/api/v1/admin/pages/{page}/publish", null)).EnsureSuccessStatusCode();

        var result = await GetAsync(factory.CreateClient(), "/api/v1/pages/by-path?path=/Trang-Thu-Public/");
        var sections = result["sections"]!.AsArray();
        sections.Should().HaveCount(1, "section bị tắt không được trả ra");
        var blocks = sections[0]!["blocks"]!.AsArray();
        blocks.Select(b => b!["type"]!.GetValue<string>()).Should().Equal("HERO", "IMAGE", "PRODUCTS");
        blocks[2]!["resolved"]!.AsArray().Should().Contain(p => p!["slug"]!.GetValue<string>() == "pub-page-product");
        result["media"]!.AsObject().Should().ContainKey(image.ToString());
    }

    [Fact]
    public async Task Navigation_blog_and_search()
    {
        var anonymous = factory.CreateClient();
        var nav = await GetAsync(anonymous, "/api/v1/site/navigation");
        nav["header"]!.AsArray().Select(i => i!["label"]!.GetValue<string>()).Should().Contain(["Sản phẩm", "Dự án", "Blog"]);

        var admin = await factory.LoginAsync();
        var categories = (await (await admin.GetAsync("/api/v1/admin/lookups")).ReadAsync<JsonObject>()).Data!["postCategories"]!.AsArray();
        var categoryId = categories.First(c => c!["extra"]!.GetValue<string>() == "react")!["id"]!.GetValue<string>();
        var post = await CreateAsync(admin, "posts", new
        {
            title = "Tối ưu React cho website doanh nghiệp", slug = "toi-uu-react-public", excerpt = "Tóm tắt bài viết",
            contentHtml = "<p>" + string.Join(" ", Enumerable.Repeat("nội dung", 120)) + "</p>",
            categoryIds = new[] { categoryId }, seo = new { },
        });
        (await admin.PostAsync($"/api/v1/admin/posts/{post}/publish", null)).EnsureSuccessStatusCode();

        var resolvedPost = await GetAsync(anonymous, "/api/v1/blog/resolve/toi-uu-react-public");
        resolvedPost["kind"]!.GetValue<string>().Should().Be("post");
        resolvedPost["post"]!["card"]!["category"]!["slug"]!.GetValue<string>().Should().Be("react");

        var resolvedCategory = await GetAsync(anonymous, "/api/v1/blog/resolve/react");
        resolvedCategory["kind"]!.GetValue<string>().Should().Be("category");

        var search = await GetAsync(anonymous, "/api/v1/search?q=React");
        search["hits"]!.AsArray().Should().Contain(h => h!["url"]!.GetValue<string>() == "/blog/toi-uu-react-public");

        (await anonymous.GetAsync("/api/v1/blog/resolve/khong-co")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<JsonObject> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadAsync<JsonObject>()).Data!;
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, string resource, object body)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/admin/{resource}", body);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        return (await response.ReadAsync<Created>()).Data!.Meta["id"]!.GetValue<Guid>();
    }

    private static async Task<Guid> UploadAsync(HttpClient admin)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(TestImages.Png(40, 30)), "files", "anh.png" } };
        var response = await admin.PostAsync("/api/v1/admin/media/upload", form);
        var json = (await response.ReadAsync<JsonArray>()).Data!;
        return json[0]!["media"]!["id"]!.GetValue<Guid>();
    }
}
