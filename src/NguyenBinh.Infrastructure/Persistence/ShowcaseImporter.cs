using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Content.Common;
using NguyenBinh.Application.Content.Projects;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Shared.Text;

namespace NguyenBinh.Infrastructure.Persistence;

/// <summary>
/// Nhap noi dung trinh bay du an (mo ta + anh man hinh that) tu thu muc: projects.json + images/&lt;slug&gt;/*.
/// Di qua dung service cua CMS (validate, phien ban, audit, upload ImageKit) — giong thao tac cua admin.
/// Chay lai an toan: anh trung checksum duoc dung lai, khong tai len lan nua.
/// Lenh: <c>dotnet NguyenBinh.Api.dll import-showcase /duong/dan/thu-muc</c>
/// </summary>
public sealed class ShowcaseImporter(
    AppDbContext db,
    IContentAdminService<Project, ProjectListItem, ProjectInput> projects,
    IMediaService media,
    ILogger<ShowcaseImporter> logger)
{
    private sealed record MediaSpec(string File, string Kind, string? Caption, string? Alt, bool Cover, bool Architecture);

    private sealed record ProjectSpec(string Slug, string Folder, bool Publish, JsonObject? Set, List<string>? Technologies,
        List<MediaSpec>? Media);

    private sealed record ShowcaseFile(List<ProjectSpec> Projects);

    public async Task<int> ImportAsync(string directory, CancellationToken ct = default)
    {
        var file = JsonSerializer.Deserialize<ShowcaseFile>(
            await File.ReadAllTextAsync(Path.Combine(directory, "projects.json"), ct), ContentJson.Options)
            ?? throw new InvalidOperationException("projects.json rỗng.");
        var technologies = await db.Set<Technology>().AsNoTracking().ToListAsync(ct);
        var failures = 0;

        foreach (var spec in file.Projects)
        {
            try
            {
                await ImportProjectAsync(directory, spec, technologies, ct);
            }
            catch (BusinessValidationException ex)
            {
                failures++;
                logger.LogError("{Slug}: {Message} {Errors}", spec.Slug, ex.Message, JsonSerializer.Serialize(ex.Errors));
            }
            catch (Exception ex) when (ex is AppException or IOException)
            {
                failures++;
                logger.LogError(ex, "{Slug}: nhap that bai", spec.Slug);
            }
        }

        return failures;
    }

    private async Task ImportProjectAsync(string directory, ProjectSpec spec, List<Technology> technologies, CancellationToken ct)
    {
        var id = await db.Set<Project>().AsNoTracking().Where(p => p.Slug == spec.Slug).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new BusinessValidationException("slug", $"Không có dự án '{spec.Slug}'.");
        var current = await projects.GetAsync(id, ct);

        // Ghi de cac truong trong "set" len du lieu form hien tai (giu nguyen moi truong khong nhac toi).
        var node = JsonSerializer.SerializeToNode(current.Data, ContentJson.Options)!.AsObject();
        foreach (var (key, value) in spec.Set ?? [])
            node[key] = value?.DeepClone();
        var input = node.Deserialize<ProjectInput>(ContentJson.Options)!;

        if (spec.Technologies is { Count: > 0 })
        {
            input.Technologies = spec.Technologies.Select(name =>
                    technologies.FirstOrDefault(t => t.Slug == Slug.From(name) || string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Where(t => t is not null).DistinctBy(t => t!.Id)
                .Select(t => new ProjectTechnologyInput { TechnologyId = t!.Id }).ToList();
            var missing = spec.Technologies.Where(n => technologies.All(t => t.Slug != Slug.From(n) && !string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)));
            foreach (var name in missing) logger.LogWarning("{Slug}: chua co cong nghe '{Name}' trong CMS — bo qua", spec.Slug, name);
        }

        if (spec.Media is { Count: > 0 })
        {
            input.Media = [];
            foreach (var m in spec.Media)
            {
                var mediaId = await UploadOnceAsync(Path.Combine(directory, "images", spec.Slug, m.File), spec.Folder, m.Alt, m.Caption, ct);
                input.Media.Add(new ProjectMediaInput
                {
                    Kind = Enum.Parse<ProjectMediaKind>(m.Kind.Replace("_", ""), ignoreCase: true),
                    MediaId = mediaId, Caption = m.Caption, Alt = m.Alt, IsPublic = true,
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

    /// <summary>Tai anh len (ImageKit) mot lan; anh cung noi dung (SHA-256) da co thi dung lai.</summary>
    private async Task<Guid> UploadOnceAsync(string path, string folder, string? alt, string? caption, CancellationToken ct)
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
        await media.UpdateAsync(m.Id, new UpdateMediaRequest(m.FileName, caption ?? alt, alt, caption, ["du-an"], m.FolderId), ct);
        logger.LogInformation("Da tai len {File} → {Url}", name, m.Url);
        return m.Id;
    }
}
