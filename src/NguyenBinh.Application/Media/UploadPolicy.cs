using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Media;

public sealed record FileTypeRule(string Extension, string MimeType, MediaKind Kind, long MaxBytes);

/// <summary>
/// Whitelist dinh dang upload. MIME luon lay tu bang nay (khong tin Content-Type cua client),
/// va noi dung phai khop chu ky (magic bytes) cua dinh dang — xem <see cref="FileSignature"/>.
/// SVG va file thuc thi khong duoc phep.
/// </summary>
public static class UploadPolicy
{
    public const long ImageMaxBytes = 15 * 1024 * 1024;
    public const long VideoMaxBytes = 200 * 1024 * 1024;
    public const long DocumentMaxBytes = 25 * 1024 * 1024;

    private static readonly Dictionary<string, FileTypeRule> Rules = new FileTypeRule[]
    {
        new(".jpg", "image/jpeg", MediaKind.Image, ImageMaxBytes),
        new(".jpeg", "image/jpeg", MediaKind.Image, ImageMaxBytes),
        new(".png", "image/png", MediaKind.Image, ImageMaxBytes),
        new(".webp", "image/webp", MediaKind.Image, ImageMaxBytes),
        new(".gif", "image/gif", MediaKind.Image, ImageMaxBytes),
        new(".avif", "image/avif", MediaKind.Image, ImageMaxBytes),
        new(".mp4", "video/mp4", MediaKind.Video, VideoMaxBytes),
        new(".webm", "video/webm", MediaKind.Video, VideoMaxBytes),
        new(".pdf", "application/pdf", MediaKind.Document, DocumentMaxBytes),
        new(".doc", "application/msword", MediaKind.Document, DocumentMaxBytes),
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", MediaKind.Document, DocumentMaxBytes),
        new(".xls", "application/vnd.ms-excel", MediaKind.Document, DocumentMaxBytes),
        new(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", MediaKind.Document, DocumentMaxBytes),
        new(".ppt", "application/vnd.ms-powerpoint", MediaKind.Document, DocumentMaxBytes),
        new(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", MediaKind.Document, DocumentMaxBytes),
        new(".zip", "application/zip", MediaKind.Document, DocumentMaxBytes),
        new(".csv", "text/csv", MediaKind.Document, DocumentMaxBytes),
        new(".txt", "text/plain", MediaKind.Document, DocumentMaxBytes),
    }.ToDictionary(r => r.Extension, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> AllowedExtensions => Rules.Keys;

    public static FileTypeRule? Find(string fileName) =>
        Rules.GetValueOrDefault(Path.GetExtension(fileName));

    /// <summary>Anh ma pipeline co the tao bien the WebP/AVIF (GIF giu nguyen de khong mat animation).</summary>
    public static bool IsResizableImage(string extension) =>
        extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".avif";
}

/// <summary>Kiem tra chu ky dau file (magic bytes) khop voi phan mo rong.</summary>
public static class FileSignature
{
    public const int HeaderLength = 512;

    public static bool Matches(string extension, ReadOnlySpan<byte> header)
    {
        if (IsExecutable(header)) return false;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".gif" => header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8),
            ".webp" => header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            ".avif" => IsFtyp(header, "avif", "avis"),
            ".mp4" => IsFtyp(header),
            ".webm" => header.StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            ".pdf" => header.StartsWith("%PDF-"u8),
            ".docx" or ".xlsx" or ".pptx" or ".zip" => header.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            ".doc" or ".xls" or ".ppt" =>
                header.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            ".csv" or ".txt" => IsPlainText(header),
            _ => false,
        };
    }

    private static bool IsExecutable(ReadOnlySpan<byte> header) =>
        header.StartsWith("MZ"u8) // PE (.exe/.dll)
        || header.StartsWith(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }) // ELF
        || header.StartsWith("#!"u8) // script
        || header.StartsWith(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }) // Mach-O / Java class
        || header.StartsWith(new byte[] { 0xCF, 0xFA, 0xED, 0xFE });

    /// <summary>ISO BMFF: "ftyp" tai offset 4, tuy chon kiem tra major brand.</summary>
    private static bool IsFtyp(ReadOnlySpan<byte> header, params string[] brands)
    {
        if (header.Length < 12 || !header[4..8].SequenceEqual("ftyp"u8)) return false;
        if (brands.Length == 0) return true;
        var major = System.Text.Encoding.ASCII.GetString(header[8..12]);
        return brands.Contains(major);
    }

    private static bool IsPlainText(ReadOnlySpan<byte> header)
    {
        if (header.Length == 0) return true;
        return !header.Contains((byte)0) && !header.TrimStart("\xEF\xBB\xBF"u8).StartsWith("<"u8);
    }
}
