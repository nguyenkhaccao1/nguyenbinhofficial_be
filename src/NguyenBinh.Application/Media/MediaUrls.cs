using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Public;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Media;

/// <summary>Tao URL anh cong khai (goc + bien the AVIF/WebP) — dung chung cho admin va website.</summary>
public interface IPublicMediaResolver
{
    Task<IReadOnlyDictionary<Guid, PublicImage>> ResolveAsync(IEnumerable<Guid?> ids, CancellationToken ct = default);
    PublicImage? ToPublic(MediaFile? media);
}

internal sealed class PublicMediaResolver(IAppDbContext db, IFileStorage storage) : IPublicMediaResolver
{
    public async Task<IReadOnlyDictionary<Guid, PublicImage>> ResolveAsync(IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var list = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, PublicImage>();

        var media = await db.MediaFiles.AsNoTracking().Where(m => list.Contains(m.Id) && !m.IsPrivate).ToListAsync(ct);
        return media.ToDictionary(m => m.Id, m => ToPublic(m)!);
    }

    public PublicImage? ToPublic(MediaFile? m)
    {
        if (m is null || m.IsPrivate) return null;
        var sources = new List<PublicImageSource>();

        if (m.Kind == MediaKind.Image && UploadPolicy.IsResizableImage(m.Extension))
        {
            if (storage.SupportsTransformations)
            {
                foreach (var w in MediaVariantProcessor.TargetWidths(m.Width ?? 0))
                foreach (var f in MediaVariantProcessor.Formats)
                    sources.Add(new PublicImageSource(f, w, storage.GetTransformedUrl(m.StorageKey, w, f)!));
            }
            else
            {
                sources.AddRange(m.Variants.Select(v => new PublicImageSource(v.Format, v.Width, storage.GetPublicUrl(v.StorageKey))));
            }
        }

        return new PublicImage(m.Id, storage.GetPublicUrl(m.StorageKey), m.Alt ?? m.Title, m.Width, m.Height, m.BlurDataUrl, sources);
    }
}
