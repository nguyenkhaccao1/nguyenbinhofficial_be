using Microsoft.Extensions.Options;
using NguyenBinh.Application.Common.Abstractions;

namespace NguyenBinh.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>"Local" (mac dinh) hoac "ImageKit".</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Thu muc goc tren dia; tuong doi so voi ContentRoot cua API.</summary>
    public string RootPath { get; set; } = "storage";

    /// <summary>Origin phuc vu media local (vd http://localhost:5080). Rong = cung domain.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string PublicRequestPath { get; set; } = "/media";

    public string PublicDirectory(string contentRoot) => Path.GetFullPath(Path.Combine(contentRoot, RootPath, "public"));
    public string PrivateDirectory(string contentRoot) => Path.GetFullPath(Path.Combine(contentRoot, RootPath, "private"));
}

/// <summary>
/// Luu file tren dia: public/ phuc vu tinh qua /media (cache 1 nam, immutable — khoa khong tai su dung),
/// private/ chi doc qua API co kiem tra quyen. Dung cho dev/test va file private o production.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _options;
    private readonly string _publicRoot;
    private readonly string _privateRoot;

    public LocalFileStorage(IOptions<StorageOptions> options, string contentRoot)
    {
        _options = options.Value;
        _publicRoot = _options.PublicDirectory(contentRoot);
        _privateRoot = _options.PrivateDirectory(contentRoot);
        Directory.CreateDirectory(_publicRoot);
        Directory.CreateDirectory(_privateRoot);
    }

    public bool SupportsTransformations => false;

    public async Task<StoredFile> SaveAsync(string key, Stream content, bool isPrivate, CancellationToken ct = default)
    {
        var path = Resolve(key, isPrivate);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(file, ct);
        return new StoredFile(key, null);
    }

    public Task<Stream?> OpenReadAsync(string key, bool isPrivate, CancellationToken ct = default)
    {
        var path = Resolve(key, isPrivate);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, string? providerFileId, bool isPrivate, CancellationToken ct = default)
    {
        var path = Resolve(key, isPrivate);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<StoredFile> MoveAsync(string key, string newKey, string? providerFileId, bool isPrivate,
        CancellationToken ct = default)
    {
        var source = Resolve(key, isPrivate);
        var target = Resolve(newKey, isPrivate);
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target, overwrite: false);
        }

        return Task.FromResult(new StoredFile(newKey, null));
    }

    public string GetPublicUrl(string key) =>
        $"{_options.PublicBaseUrl.TrimEnd('/')}{_options.PublicRequestPath}/{key}";

    public string? GetTransformedUrl(string key, int width, string format) => null;

    /// <summary>Chan path traversal: duong dan cuoi cung bat buoc nam trong thu muc goc.</summary>
    private string Resolve(string key, bool isPrivate)
    {
        var root = isPrivate ? _privateRoot : _publicRoot;
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage key không hợp lệ.");
        return full;
    }
}
