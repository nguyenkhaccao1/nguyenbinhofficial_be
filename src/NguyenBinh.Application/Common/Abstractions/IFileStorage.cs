namespace NguyenBinh.Application.Common.Abstractions;

/// <summary>Ket qua luu file: khoa (duong dan) va id phia nha cung cap (ImageKit can fileId de xoa/di chuyen).</summary>
public sealed record StoredFile(string Key, string? ProviderFileId);

/// <summary>
/// Luu tru file. Khoa la duong dan dang cay de doc (vd "nguyenbinhofficial/du-an/perfectkey/dashboard-a1b2c3.png").
/// Trien khai: Local (dev/test) va ImageKit (production, CDN + resize/WebP/AVIF tai CDN). File private luon o local.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(string key, Stream content, bool isPrivate, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, bool isPrivate, CancellationToken ct = default);
    Task DeleteAsync(string key, string? providerFileId, bool isPrivate, CancellationToken ct = default);

    /// <summary>Doi vi tri file (khi chuyen thu muc). Tra ve khoa moi.</summary>
    Task<StoredFile> MoveAsync(string key, string newKey, string? providerFileId, bool isPrivate, CancellationToken ct = default);

    string GetPublicUrl(string key);

    /// <summary>
    /// true: CDN tu resize/doi dinh dang qua URL → khong can sinh bien the tai server.
    /// </summary>
    bool SupportsTransformations { get; }

    /// <summary>URL anh da resize + doi dinh dang (chi khi SupportsTransformations).</summary>
    string? GetTransformedUrl(string key, int width, string format);
}
