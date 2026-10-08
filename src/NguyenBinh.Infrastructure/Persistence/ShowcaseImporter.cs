using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Content.Library;
using NguyenBinh.Application.Content.Pages;
using NguyenBinh.Application.Content.Projects;
using NguyenBinh.Application.Content.Services;
using NguyenBinh.Application.Media;
using NguyenBinh.Application.Settings;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Shared.Text;

namespace NguyenBinh.Infrastructure.Persistence;

/// <summary>
/// Nhap noi dung trinh bay (du an, dich vu, doi tac, nhan dien thuong hieu) tu thu muc: projects.json + images/&lt;nhom&gt;/*.
/// Di qua dung service cua CMS (validate, phien ban, audit, upload ImageKit) — giong thao tac cua admin.
/// Chay lai an toan: anh trung checksum duoc dung lai; dich vu/doi tac da co thi cap nhat thay vi tao moi.
/// Lenh: <c>dotnet NguyenBinh.Api.dll import-showcase /duong/dan/thu-muc</c>
/// </summary>
public sealed partial class ShowcaseImporter(
    AppDbContext db,
    IContentAdminService<Project, ProjectListItem, ProjectInput> projects,
    IContentAdminService<Page, PageListItem, PageInput> pages,
    IContentAdminService<Service, ServiceListItem, ServiceInput> services,
    IContentAdminService<Partner, LibraryListItem, PartnerInput> partners,
    IMediaService media,
    IPublicMediaResolver mediaUrls,
    ISettingsService settings,
    ILogger<ShowcaseImporter> logger)
{
    private static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web);

    // Hidden: chi dung lam anh bia/kien truc, khong hien trong bo anh cua trang chi tiet.
    private sealed record MediaSpec(string File, string Kind, string? Caption, string? Alt, bool Cover, bool Architecture, bool Hidden);

    private sealed record ProjectSpec(string Slug, string Folder, bool Publish, JsonObject? Set, List<string>? Technologies,
        List<MediaSpec>? Media);

    private sealed record PageSpec(string Path);

    private sealed record CategorySpec(string Slug, string Name, int SortOrder);

    // Images: anh dung trong noi dung — chuoi "{{img:ten-file.jpg}}" trong "set" duoc thay bang URL anh tren CDN.
    private sealed record ServiceSpec(string Slug, string Name, string Category, string Folder, bool Publish, string? Cover,
        List<string>? Images, List<string>? RelatedProjects, List<string>? Technologies, JsonObject? Set);

    private sealed record PartnerSpec(string Name, string? Url, string? Kind, string? Logo, int SortOrder);

    // Settings: { "brand": { "set": {...}, "media": { "logo": "file.png" } }, "theme": {...}, "seo": {...} }
    private sealed record SettingSpec(JsonObject? Set, Dictionary<string, string>? Media);

    // Trang page builder: tim theo Path — co thi cap nhat (ghi de cac truong trong Set), chua co thi tao moi.
    private sealed record PageContentSpec(string Path, bool Publish, JsonObject Set);

    private sealed record ShowcaseFile(List<ProjectSpec>? Projects, List<PageSpec>? PublishPages, List<CategorySpec>? ServiceCategories,
        List<ServiceSpec>? Services, List<PartnerSpec>? Partners, Dictionary<string, SettingSpec>? Settings, List<PageContentSpec>? Pages);

    public async Task<int> ImportAsync(string directory, CancellationToken ct = default)
    {
        var file = JsonSerializer.Deserialize<ShowcaseFile>(
            await File.ReadAllTextAsync(Path.Combine(directory, "projects.json"), ct), ContentJson.Options)
            ?? throw new InvalidOperationException("projects.json rỗng.");
        var technologies = await db.Set<Technology>().AsNoTracking().ToListAsync(ct);
        var failures = 0;

        async Task Run(string label, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (BusinessValidationException ex)
            {
                failures++;
                logger.LogError("{Label}: {Message} {Errors}", label, ex.Message, JsonSerializer.Serialize(ex.Errors));
            }
            catch (Exception ex) when (ex is AppException or IOException or FluentValidation.ValidationException)
            {
                failures++;
                logger.LogError(ex, "{Label}: nhap that bai", label);
            }
        }

        foreach (var (key, spec) in file.Settings ?? [])
            await Run($"settings.{key}", () => ImportSettingAsync(directory, key, spec, ct));
        foreach (var spec in file.Projects ?? [])
            await Run(spec.Slug, () => ImportProjectAsync(directory, spec, technologies, ct));
        foreach (var spec in file.ServiceCategories ?? [])
            await Run($"category {spec.Slug}", () => EnsureServiceCategoryAsync(spec, ct));
        foreach (var spec in file.Services ?? [])
            await Run($"service {spec.Slug}", () => ImportServiceAsync(directory, spec, technologies, ct));
        foreach (var spec in file.Partners ?? [])
            await Run($"partner {spec.Name}", () => ImportPartnerAsync(directory, spec, ct));

        foreach (var spec in file.Pages ?? [])
            await Run($"page {spec.Path}", () => ImportPageAsync(spec, ct));

        // Trang (vd trang chu seed san) chi xuat ban sau khi da co du lieu that cho cac block dong.
        foreach (var page in file.PublishPages ?? [])
        {
            await Run($"page {page.Path}", async () =>
            {
                var pageEntity = await db.Set<Page>().AsNoTracking().Where(p => p.Path == page.Path)
                    .Select(p => new { p.Id, p.Status }).FirstOrDefaultAsync(ct)
                    ?? throw new BusinessValidationException("path", $"Không có trang {page.Path}.");
                if (pageEntity.Status == ContentStatus.Published) return;
                await pages.PublishAsync(pageEntity.Id, ct);
                logger.LogInformation("Trang {Path}: da xuat ban", page.Path);
            });
        }

        return failures;
    }

    // ------------------------------------------------------------------ Du an

    private async Task ImportProjectAsync(string directory, ProjectSpec spec, List<Technology> technologies, CancellationToken ct)
    {
        var id = await db.Set<Project>().AsNoTracking().Where(p => p.Slug == spec.Slug).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new BusinessValidationException("slug", $"Không có dự án '{spec.Slug}'.");
        var current = await projects.GetAsync(id, ct);
        var input = Merge(current.Data, spec.Set);

        if (spec.Technologies is { Count: > 0 })
            input.Technologies = ResolveTechnologies(spec.Slug, spec.Technologies, technologies)
                .Select(t => new ProjectTechnologyInput { TechnologyId = t }).ToList();

        if (spec.Media is { Count: > 0 })
        {
            input.Media = [];
            foreach (var m in spec.Media)
            {
                var mediaId = await UploadOnceAsync(Path.Combine(directory, "images", spec.Slug, m.File), spec.Folder, m.Alt, m.Caption, "du-an", ct);
                input.Media.Add(new ProjectMediaInput
                {
                    Kind = Enum.Parse<ProjectMediaKind>(m.Kind.Replace("_", ""), ignoreCase: true),
                    MediaId = mediaId, Caption = m.Caption, Alt = m.Alt, IsPublic = !m.Hidden,
                });
                if (m.Cover) input.CoverMediaId = input.ThumbnailMediaId = mediaId;
                if (m.Architecture) input.ArchitectureMediaId = mediaId;
            }
        }

        var saved = await projects.UpdateAsync(id, input, current.Meta.RowVersion, ct);
        logger.LogInformation("{Slug}: da cap nhat ({Media} anh)", spec.Slug, input.Media.Count);

        if (spec.Publish && saved.Meta.Status != ContentStatus.Published)
        {
            await projects.PublishAsync(id, ct);
            logger.LogInformation("{Slug}: da xuat ban", spec.Slug);
        }
    }

    // ------------------------------------------------------------------ Dich vu

    private async Task EnsureServiceCategoryAsync(CategorySpec spec, CancellationToken ct)
    {
        var category = await db.Set<ServiceCategory>().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Slug == spec.Slug, ct);
        if (category is null)
        {
            db.Set<ServiceCategory>().Add(new ServiceCategory { Name = spec.Name, Slug = spec.Slug, SortOrder = spec.SortOrder });
            logger.LogInformation("Nhom dich vu {Slug}: da tao", spec.Slug);
        }
        else
        {
            category.Name = spec.Name;
            category.SortOrder = spec.SortOrder;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ImportServiceAsync(string directory, ServiceSpec spec, List<Technology> technologies, CancellationToken ct)
    {
        var folder = Path.Combine(directory, "images", spec.Slug);
        var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in spec.Images ?? [])
        {
            var id = await UploadOnceAsync(Path.Combine(folder, image), spec.Folder, null, null, "dich-vu", ct);
            urls[image] = (await mediaUrls.ResolveAsync([id], ct))[id].Url;
        }

        var set = (JsonObject?)ReplaceImages(spec.Set, urls) ?? [];
        set["name"] = spec.Name;
        set["slug"] = spec.Slug;
        set["categoryId"] = await db.Set<ServiceCategory>().AsNoTracking().Where(c => c.Slug == spec.Category)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct)
            ?? throw new BusinessValidationException("category", $"Không có nhóm dịch vụ '{spec.Category}'.");
        if (spec.Cover is not null)
            set["coverMediaId"] = await UploadOnceAsync(Path.Combine(folder, spec.Cover), spec.Folder, spec.Name, null, "dich-vu", ct);
        if (spec.RelatedProjects is { Count: > 0 })
        {
            var ids = await db.Set<Project>().AsNoTracking().Where(p => spec.RelatedProjects.Contains(p.Slug))
                .Select(p => new { p.Id, p.Slug }).ToListAsync(ct);
            set["relatedProjectIds"] = new JsonArray(spec.RelatedProjects.Select(s => ids.FirstOrDefault(p => p.Slug == s)?.Id)
                .Where(id => id is not null).Select(id => (JsonNode)JsonValue.Create(id!.Value)).ToArray());
        }
        if (spec.Technologies is { Count: > 0 })
            set["technologyIds"] = new JsonArray(ResolveTechnologies(spec.Slug, spec.Technologies, technologies)
                .Select(id => (JsonNode)JsonValue.Create(id)).ToArray());

        var existingId = await db.Set<Service>().AsNoTracking().Where(s => s.Slug == spec.Slug).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        ContentDetail<ServiceInput> saved;
        if (existingId is { } id2)
        {
            var current = await services.GetAsync(id2, ct);
            saved = await services.UpdateAsync(id2, Merge(current.Data, set), current.Meta.RowVersion, ct);
        }
        else
        {
            saved = await services.CreateAsync(Merge(new ServiceInput(), set), ct);
        }
        logger.LogInformation("Dich vu {Slug}: da luu ({Images} anh noi dung)", spec.Slug, urls.Count);

        if (spec.Publish && saved.Meta.Status != ContentStatus.Published)
        {
            await services.PublishAsync(saved.Meta.Id, ct);
            logger.LogInformation("Dich vu {Slug}: da xuat ban", spec.Slug);
        }
    }

    // ------------------------------------------------------------------ Trang

    private async Task ImportPageAsync(PageContentSpec spec, CancellationToken ct)
    {
        var set = (JsonObject)spec.Set.DeepClone();
        set["path"] = spec.Path;
        var existingId = await db.Set<Page>().AsNoTracking().Where(p => p.Path == spec.Path).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        ContentDetail<PageInput> saved;
        if (existingId is { } id)
        {
            var current = await pages.GetAsync(id, ct);
            saved = await pages.UpdateAsync(id, Merge(current.Data, set), current.Meta.RowVersion, ct);
        }
        else
        {
            saved = await pages.CreateAsync(Merge(new PageInput(), set), ct);
        }
        logger.LogInformation("Trang {Path}: da luu", spec.Path);

        if (spec.Publish && saved.Meta.Status != ContentStatus.Published)
        {
            await pages.PublishAsync(saved.Meta.Id, ct);
            logger.LogInformation("Trang {Path}: da xuat ban", spec.Path);
        }
    }

    // ------------------------------------------------------------------ Doi tac

    private async Task ImportPartnerAsync(string directory, PartnerSpec spec, CancellationToken ct)
    {
        var input = new PartnerInput { Name = spec.Name, Url = spec.Url, Kind = spec.Kind, SortOrder = spec.SortOrder };
        if (spec.Logo is not null)
            input.LogoMediaId = await UploadOnceAsync(Path.Combine(directory, "images", "_partners", spec.Logo), "Đối tác", spec.Name, null, "doi-tac", ct);

        var existingId = await db.Set<Partner>().AsNoTracking().Where(p => p.Name == spec.Name).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        ContentDetail<PartnerInput> saved;
        if (existingId is { } id)
        {
            var current = await partners.GetAsync(id, ct);
            saved = await partners.UpdateAsync(id, input, current.Meta.RowVersion, ct);
        }
        else
        {
            saved = await partners.CreateAsync(input, ct);
        }

        if (saved.Meta.Status != ContentStatus.Published) await partners.PublishAsync(saved.Meta.Id, ct);
        logger.LogInformation("Doi tac {Name}: da luu", spec.Name);
    }

    // ------------------------------------------------------------------ Cau hinh (thuong hieu, mau, SEO)

    private async Task ImportSettingAsync(string directory, string key, SettingSpec spec, CancellationToken ct)
    {
        var node = JsonSerializer.SerializeToNode(await settings.GetGroupAsync(key, ct), SettingsJson)!.AsObject();
        foreach (var (field, value) in spec.Set ?? [])
            node[field] = value?.DeepClone();
        foreach (var (field, fileName) in spec.Media ?? [])
        {
            var id = await UploadOnceAsync(Path.Combine(directory, "images", "_brand", fileName), "Thương hiệu", node["siteName"]?.GetValue<string>(), null, "thuong-hieu", ct);
            node[field] = new JsonObject { ["id"] = id };
        }

        await settings.UpdateGroupAsync(key, JsonSerializer.SerializeToElement(node, SettingsJson), ct);
        logger.LogInformation("Cau hinh {Key}: da cap nhat", key);
    }

    // ------------------------------------------------------------------ Tien ich

    /// <summary>Ghi de cac truong trong "set" len du lieu form hien tai (giu nguyen moi truong khong nhac toi).</summary>
    private static T Merge<T>(T current, JsonObject? set) where T : class
    {
        var node = JsonSerializer.SerializeToNode(current, ContentJson.Options)!.AsObject();
        foreach (var (key, value) in set ?? [])
            node[key] = value?.DeepClone();
        return node.Deserialize<T>(ContentJson.Options)!;
    }

    private static JsonNode? ReplaceImages(JsonNode? node, IReadOnlyDictionary<string, string> urls) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, ReplaceImages(p.Value, urls)))),
        JsonArray arr => new JsonArray(arr.Select(n => ReplaceImages(n, urls)).ToArray()),
        JsonValue v when v.TryGetValue<string>(out var s) => JsonValue.Create(ImagePlaceholder().Replace(s,
            m => urls.TryGetValue(m.Groups[1].Value, out var url) ? url : throw new BusinessValidationException("images", $"Thiếu ảnh {m.Groups[1].Value} trong danh sách images."))),
        _ => node?.DeepClone(),
    };

    [GeneratedRegex(@"\{\{img:([^}]+)\}\}")]
    private static partial Regex ImagePlaceholder();

    private List<Guid> ResolveTechnologies(string owner, List<string> names, List<Technology> technologies)
    {
        var result = new List<Guid>();
        foreach (var name in names)
        {
            var tech = technologies.FirstOrDefault(t => t.Slug == Slug.From(name) || string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tech is null) logger.LogWarning("{Owner}: chua co cong nghe '{Name}' trong CMS — bo qua", owner, name);
            else if (!result.Contains(tech.Id)) result.Add(tech.Id);
        }

        return result;
    }

    /// <summary>Tai anh len (ImageKit) mot lan; anh cung noi dung (SHA-256) da co thi dung lai.</summary>
    private async Task<Guid> UploadOnceAsync(string path, string folder, string? alt, string? caption, string tag, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));
        var existing = await db.MediaFiles.AsNoTracking().Where(f => f.Checksum == checksum).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct);
        if (existing is { } found) return found;

        var name = Path.GetFileName(path);
        var result = (await media.UploadAsync([new UploadFile(name, bytes.Length, () => new MemoryStream(bytes))], null, folder, ct))[0];
        if (!result.Success || result.Media is null)
            throw new BusinessValidationException("media", $"{name}: {result.Error}");

        var m = result.Media;
        await media.UpdateAsync(m.Id, new UpdateMediaRequest(m.FileName, caption ?? alt, alt, caption, [tag], m.FolderId), ct);
        logger.LogInformation("Da tai len {File} → {Url}", name, m.Url);
        return m.Id;
    }
}
