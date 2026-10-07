using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Media;
using NguyenBinh.Shared.Text;

namespace NguyenBinh.Application.Media;

public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>Thu muc goc tren kho luu tru — tach moi truong (vd "nguyenbinhofficial", "nguyenbinhofficial-dev").</summary>
    public string KeyPrefix { get; set; } = "nguyenbinhofficial";

    /// <summary>Thu muc cho file tai len o thu muc goc cua thu vien.</summary>
    public string DefaultFolder { get; set; } = "chung";
}

/// <summary>
/// Sinh khoa luu tru de doc, dang cay theo thu muc media:
/// "{prefix}/{thu-muc}/{thu-muc-con}/{ten-file}-{6 ky tu}.{ext}" — vd
/// "nguyenbinhofficial/du-an/perfectkey-workforce/man-hinh-dashboard-k3f9qa.png".
/// Hau to ngan chong trung ten ma van giu ten file ro rang.
/// </summary>
public static class MediaKeys
{
    public static string Build(MediaOptions options, IReadOnlyList<string> folderSlugs, string fileName, string extension)
    {
        var folders = folderSlugs.Count > 0 ? folderSlugs : [options.DefaultFolder];
        var name = Slug.From(Path.GetFileNameWithoutExtension(fileName), 80);
        if (name.Length == 0) name = "file";
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{Slug.From(options.KeyPrefix)}/{string.Join('/', folders)}/{name}-{suffix}{extension.ToLowerInvariant()}";
    }

    /// <summary>Doi thu muc cua khoa, giu nguyen ten file.</summary>
    public static string Relocate(MediaOptions options, string key, IReadOnlyList<string> folderSlugs) =>
        $"{Slug.From(options.KeyPrefix)}/{string.Join('/', folderSlugs.Count > 0 ? folderSlugs : [options.DefaultFolder])}/{key[(key.LastIndexOf('/') + 1)..]}";

    /// <summary>Chuoi slug tu goc toi thu muc (vd ["du-an", "perfectkey-workforce"]).</summary>
    public static async Task<IReadOnlyList<string>> FolderSlugsAsync(IAppDbContext db, Guid? folderId, CancellationToken ct)
    {
        if (folderId is null) return [];
        var folders = await db.MediaFolders.AsNoTracking().Select(f => new { f.Id, f.Name, f.ParentId }).ToListAsync(ct);
        var path = new List<string>();
        Guid? cursor = folderId;
        while (cursor is { } id && path.Count < 10)
        {
            var folder = folders.FirstOrDefault(f => f.Id == id);
            if (folder is null) break;
            path.Insert(0, Slug.From(folder.Name, 60) is { Length: > 0 } s ? s : "thu-muc");
            cursor = folder.ParentId;
        }

        return path;
    }

    /// <summary>
    /// Tao (neu chua co) chuoi thu muc theo ten hien thi, vd "Dự án/PerfectKey Workforce".
    /// Dung khi upload tu form noi dung de anh tu vao dung nhanh cay.
    /// </summary>
    public static async Task<Guid?> EnsureFolderPathAsync(IAppDbContext db, string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var names = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.Length > 100 ? n[..100] : n).Take(6).ToList();

        Guid? parentId = null;
        foreach (var name in names)
        {
            var existing = await db.MediaFolders.FirstOrDefaultAsync(f => f.ParentId == parentId && f.Name == name, ct);
            if (existing is null)
            {
                existing = new MediaFolder { Name = name, ParentId = parentId };
                db.MediaFolders.Add(existing);
                await db.SaveChangesAsync(ct);
            }

            parentId = existing.Id;
        }

        return parentId;
    }
}
