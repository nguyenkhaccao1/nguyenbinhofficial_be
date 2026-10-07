using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Media;

/// <summary>
/// Tao bien the WebP + AVIF theo cac do rong chuan cho srcset (chay nen, goi tu worker).
/// </summary>
public interface IMediaVariantProcessor
{
    Task ProcessAsync(Guid mediaId, CancellationToken ct = default);
}

internal sealed class MediaVariantProcessor(
    IAppDbContext db,
    IFileStorage storage,
    IImageProcessor images,
    ILogger<MediaVariantProcessor> logger) : IMediaVariantProcessor
{
    internal static readonly int[] Widths = [320, 640, 960, 1280, 1920];
    internal static readonly string[] Formats = ["webp", "avif"];

    public async Task ProcessAsync(Guid mediaId, CancellationToken ct = default)
    {
        var media = await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == mediaId, ct);
        if (media is null || media.Kind != MediaKind.Image || !UploadPolicy.IsResizableImage(media.Extension)) return;
        if (storage.SupportsTransformations)
        {
            media.ProcessingState = MediaProcessingState.Done;
            await db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            byte[] bytes;
            await using (var s = await storage.OpenReadAsync(media.StorageKey, media.IsPrivate, ct)
                                 ?? throw new FileNotFoundException(media.StorageKey))
            using (var ms = new MemoryStream())
            {
                await s.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var variants = new List<MediaVariant>();
            var baseKey = media.StorageKey[..media.StorageKey.LastIndexOf('.')];
            foreach (var width in TargetWidths(media.Width ?? 0))
            foreach (var format in Formats)
            {
                ct.ThrowIfCancellationRequested();
                var encoded = images.Resize(bytes, width, format);
                var key = $"{baseKey}-w{width}.{format}";
                var stored = await storage.SaveAsync(key, new MemoryStream(encoded.Bytes), media.IsPrivate, ct);
                variants.Add(new MediaVariant
                {
                    Format = format, Width = encoded.Width, Height = encoded.Height, StorageKey = stored.Key,
                    SizeBytes = encoded.Bytes.LongLength,
                });
            }

            media.Variants = variants;
            media.ProcessingState = MediaProcessingState.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tao bien the anh that bai cho media {MediaId}", mediaId);
            media.ProcessingState = MediaProcessingState.Failed;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cac do rong nho hon anh goc, cong them chinh do rong goc (toi da 1920) de khong phong to anh.</summary>
    internal static IReadOnlyList<int> TargetWidths(int originalWidth)
    {
        if (originalWidth <= 0) return [];
        var widths = Widths.Where(w => w < originalWidth).ToList();
        var largest = Math.Min(originalWidth, Widths[^1]);
        if (!widths.Contains(largest)) widths.Add(largest);
        return widths;
    }
}
