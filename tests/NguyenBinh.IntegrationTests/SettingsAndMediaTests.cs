using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NguyenBinh.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SettingsAndMediaTests(ApiFactory factory)
{
    [Fact]
    public async Task Public_settings_expose_only_public_groups()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/site/settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadAsync<Dictionary<string, JsonElement>>();
        body.Data!.Keys.Should().Contain(["brand", "theme", "contact", "tracking", "seo"]).And.NotContain("forms");
        body.Data["brand"].GetProperty("siteName").GetString().Should().Be("Nguyên Bình Technology");
    }

    [Fact]
    public async Task Updating_settings_validates_and_refreshes_public_cache()
    {
        var admin = await factory.LoginAsync();

        var invalid = await admin.PutAsJsonAsync("/api/v1/admin/settings/tracking", new { ga4MeasurementId = "UA-123" });
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.ReadAsync<object>()).Errors.Should().ContainKey("ga4MeasurementId");

        (await admin.PutAsJsonAsync("/api/v1/admin/settings/social", new { facebook = "https://facebook.com/nguyenbinh" }))
            .EnsureSuccessStatusCode();

        var publicSettings = await (await factory.CreateClient().GetAsync("/api/v1/site/settings"))
            .ReadAsync<Dictionary<string, JsonElement>>();
        publicSettings.Data!["social"].GetProperty("facebook").GetString().Should().Be("https://facebook.com/nguyenbinh");
    }

    [Fact]
    public async Task Unknown_settings_group_is_404()
    {
        var admin = await factory.LoginAsync();
        (await admin.GetAsync("/api/v1/admin/settings/nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_accepts_valid_files_and_rejects_disguised_or_dangerous_ones()
    {
        var admin = await factory.LoginAsync();
        using var form = new MultipartFormDataContent
        {
            { File(TestImages.Png(64, 48)), "files", "ok.png" },
            { File("MZ\u0090\0 not really a png"u8.ToArray()), "files", "fake.png" },
            { File("<svg onload=alert(1)>"u8.ToArray()), "files", "evil.svg" },
            { File(Zip()), "files", "brochure.docx" },
        };

        var response = await admin.PostAsync("/api/v1/admin/media/upload", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = (await response.ReadAsync<List<UploadResult>>()).Data!;

        results.Single(r => r.FileName == "ok.png").Success.Should().BeTrue();
        results.Single(r => r.FileName == "ok.png").Media!.Width.Should().Be(64);
        results.Single(r => r.FileName == "fake.png").Success.Should().BeFalse();
        results.Single(r => r.FileName == "evil.svg").Success.Should().BeFalse();
        results.Single(r => r.FileName == "brochure.docx").Media!.Kind.Should().Be("DOCUMENT");

        var url = results.Single(r => r.FileName == "ok.png").Media!.Url;
        url.Should().StartWith("/media/");
        var file = await factory.CreateClient().GetAsync(url);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task Media_in_use_cannot_be_deleted_without_force()
    {
        var admin = await factory.LoginAsync();
        using var form = new MultipartFormDataContent { { File(TestImages.Png(32, 32)), "files", "logo.png" } };
        var media = (await (await admin.PostAsync("/api/v1/admin/media/upload", form)).ReadAsync<List<UploadResult>>())
            .Data!.Single().Media!;

        (await admin.PutAsJsonAsync("/api/v1/admin/settings/brand", new
        {
            siteName = "Nguyên Bình Technology", shortName = "Nguyên Bình", logo = new { id = media.Id },
        })).EnsureSuccessStatusCode();

        var publicBrand = await (await factory.CreateClient().GetAsync("/api/v1/site/settings"))
            .ReadAsync<Dictionary<string, JsonElement>>();
        publicBrand.Data!["brand"].GetProperty("logo").GetProperty("url").GetString().Should().EndWith(".png");

        var blocked = await admin.DeleteAsync($"/api/v1/admin/media/{media.Id}");
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var details = await blocked.ReadAsync<JsonElement>();
        details.Data.GetProperty("usages")[0].GetProperty("entityType").GetString().Should().Be("SETTINGS");

        (await admin.DeleteAsync($"/api/v1/admin/media/{media.Id}?force=true")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/admin/media/{media.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Xoa xong, logo khong con tro toi file da xoa.
        (await admin.PutAsJsonAsync("/api/v1/admin/settings/brand",
            new { siteName = "Nguyên Bình Technology", shortName = "Nguyên Bình" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Folders_reject_duplicates_cycles_and_non_empty_delete()
    {
        var admin = await factory.LoginAsync();
        var parent = (await (await admin.PostAsJsonAsync("/api/v1/admin/media/folders", new { name = "Dự án" }))
            .ReadAsync<Folder>()).Data!;
        var child = (await (await admin.PostAsJsonAsync("/api/v1/admin/media/folders",
            new { name = "PerfectKey", parentId = parent.Id })).ReadAsync<Folder>()).Data!;

        (await admin.PostAsJsonAsync("/api/v1/admin/media/folders", new { name = "Dự án" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PutAsJsonAsync($"/api/v1/admin/media/folders/{parent.Id}", new { name = "Dự án", parentId = child.Id }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await admin.DeleteAsync($"/api/v1/admin/media/folders/{parent.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await admin.DeleteAsync($"/api/v1/admin/media/folders/{child.Id}")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/v1/admin/media/folders/{parent.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Changes_are_written_to_audit_log()
    {
        var admin = await factory.LoginAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/settings/contact", new { phone = "0900 111 222" }))
            .EnsureSuccessStatusCode();

        var logs = await (await admin.GetAsync("/api/v1/admin/audit-logs?entityType=SiteSetting&pageSize=5"))
            .ReadAsync<Paged<AuditRow>>();
        logs.Data!.Items.Should().Contain(l => l.Action == "UPDATE" && l.NewValues!.Contains("0900 111 222")
                                               && l.UserName == ApiFactory.AdminEmail);
    }

    [Fact]
    public async Task Health_endpoints_are_public()
    {
        var client = factory.CreateClient();
        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static ByteArrayContent File(byte[] bytes) => new(bytes);

    private static byte[] Zip()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            zip.CreateEntry("word/document.xml");
        return ms.ToArray();
    }

    private sealed record UploadResult(string FileName, bool Success, MediaRow? Media, string? Error);

    private sealed record MediaRow(Guid Id, string Url, string Kind, int? Width, int? Height);

    private sealed record Folder(Guid Id, string Name, Guid? ParentId);

    private sealed record AuditRow(string Action, string? EntityType, string? UserName, string? NewValues);
}

internal static class TestImages
{
    /// <summary>PNG RGB hop le toi thieu (khong can thu vien anh).</summary>
    public static byte[] Png(int width, int height)
    {
        var raw = new byte[height * (width * 3 + 1)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = y * (width * 3 + 1) + 1 + x * 3;
            raw[i] = (byte)(x * 255 / width);
            raw[i + 1] = (byte)(y * 255 / height);
            raw[i + 2] = 180;
        }

        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB
        Chunk(ms, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, true)) z.Write(raw);
            Chunk(ms, "IDAT", compressed.ToArray());
        }

        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteInt(len, 0, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32([.. typeBytes, .. data]));
        s.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
