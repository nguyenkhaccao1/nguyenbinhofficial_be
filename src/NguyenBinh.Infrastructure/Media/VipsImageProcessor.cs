using NetVips;
using NguyenBinh.Application.Common.Abstractions;

namespace NguyenBinh.Infrastructure.Media;

/// <summary>
/// Xu ly anh bang libvips (nhanh, it RAM, ho tro WebP + AVIF). Luon xoay theo EXIF va bo metadata
/// (GPS, thong tin may anh) khi xuat.
/// </summary>
internal sealed class VipsImageProcessor : IImageProcessor
{
    // Gioi han chieu cao "vo cuc" de thumbnail chi rang buoc theo chieu rong.
    private const int Unbounded = 100_000;

    public ImageInfo? Probe(byte[] image)
    {
        try
        {
            using var img = Image.NewFromBuffer(image, access: Enums.Access.Sequential);
            var orientation = img.Contains("orientation") ? (int)img.Get("orientation") : 1;
            return orientation >= 5 ? new ImageInfo(img.Height, img.Width) : new ImageInfo(img.Width, img.Height);
        }
        catch (VipsException)
        {
            return null;
        }
    }

    public EncodedImage Resize(byte[] image, int width, string format)
    {
        using var thumb = Image.ThumbnailBuffer(image, width, height: Unbounded, size: Enums.Size.Down);
        return new EncodedImage(Encode(thumb, format), thumb.Width, thumb.Height);
    }

    public string CreateBlurDataUrl(byte[] image)
    {
        using var thumb = Image.ThumbnailBuffer(image, 16, height: Unbounded, size: Enums.Size.Down);
        var bytes = thumb.WebpsaveBuffer(q: 40, keep: Enums.ForeignKeep.None);
        return "data:image/webp;base64," + Convert.ToBase64String(bytes);
    }

    public EncodedImage Crop(byte[] image, int x, int y, int width, int height, string format)
    {
        using var source = Image.NewFromBuffer(image);
        using var rotated = source.Autorot();
        using var cropped = rotated.Crop(x, y, width, height);
        return new EncodedImage(Encode(cropped, format), cropped.Width, cropped.Height);
    }

    private static byte[] Encode(Image image, string format) => format.ToLowerInvariant() switch
    {
        "webp" => image.WebpsaveBuffer(q: 80, effort: 4, keep: Enums.ForeignKeep.None),
        "avif" => image.HeifsaveBuffer(q: 55, compression: Enums.ForeignHeifCompression.Av1, effort: 4,
            keep: Enums.ForeignKeep.None),
        "jpg" or "jpeg" => image.JpegsaveBuffer(q: 86, optimizeCoding: true, interlace: true,
            keep: Enums.ForeignKeep.None),
        "png" => image.PngsaveBuffer(compression: 6, keep: Enums.ForeignKeep.None),
        _ => throw new NotSupportedException($"Định dạng ảnh không hỗ trợ: {format}"),
    };
}
