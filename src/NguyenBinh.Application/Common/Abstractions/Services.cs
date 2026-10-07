using NguyenBinh.Domain.Identity;

namespace NguyenBinh.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }

    bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Cache phan tan (Memory o Development, Redis o SIT/UAT/Production).</summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl,
        CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user, IReadOnlyCollection<string> roles);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Luu tru file (Local disk → S3/Azure Blob sau nay). Key dang "2026/10/{id}.jpg".</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, bool isPrivate, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, bool isPrivate, CancellationToken ct = default);
    Task DeleteAsync(string key, bool isPrivate, CancellationToken ct = default);

    /// <summary>URL public (CDN-ready) cho file khong private.</summary>
    string GetPublicUrl(string key);
}

public interface IImageProcessor
{
    /// <summary>Doc kich thuoc anh (da tinh xoay EXIF). Null neu khong doc duoc.</summary>
    ImageInfo? Probe(byte[] image);

    /// <summary>Thu nho ve chieu rong <paramref name="width"/> va encode sang "webp" | "avif".</summary>
    EncodedImage Resize(byte[] image, int width, string format);

    /// <summary>Anh rat nho (data URL) lam placeholder blur.</summary>
    string CreateBlurDataUrl(byte[] image);

    EncodedImage Crop(byte[] image, int x, int y, int width, int height, string format);
}

public sealed record ImageInfo(int Width, int Height);

public sealed record EncodedImage(byte[] Bytes, int Width, int Height);

/// <summary>Hang doi tao bien the anh (WebP/AVIF) chay nen de upload tra ve nhanh.</summary>
public interface IMediaProcessingQueue
{
    ValueTask EnqueueAsync(Guid mediaId, CancellationToken ct = default);
}

/// <summary>Diem moc quet malware (mac dinh no-op; co the gan ClamAV).</summary>
public interface IMalwareScanner
{
    Task<bool> IsCleanAsync(Stream content, string fileName, CancellationToken ct = default);
}
