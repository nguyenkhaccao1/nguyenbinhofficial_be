using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Media;

public class MediaFolder : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public MediaFolder? Parent { get; set; }
}

public class MediaFile : AuditableEntity
{
    public Guid? FolderId { get; set; }
    public MediaFolder? Folder { get; set; }

    /// <summary>Ten hien thi (doi ten trong admin chi doi truong nay, URL giu nguyen).</summary>
    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Khoa luu tru (vd "2026/10/{id}.jpg"). URL duoc tinh tu khoa luc doc de doi CDN khong phai sua DB.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public MediaKind Kind { get; set; }
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public string? Title { get; set; }
    public string? Alt { get; set; }
    public string? Caption { get; set; }
    public List<string> Tags { get; set; } = [];

    public List<MediaVariant> Variants { get; set; } = [];
    public MediaProcessingState ProcessingState { get; set; } = MediaProcessingState.None;

    /// <summary>Anh nho (data URL webp ~16px) lam placeholder blur.</summary>
    public string? BlurDataUrl { get; set; }
    public string Checksum { get; set; } = string.Empty;

    /// <summary>File private (vd dinh kem lead) khong phuc vu qua /media, chi tai qua API co quyen.</summary>
    public bool IsPrivate { get; set; }
}

public class MediaVariant
{
    public string Format { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

/// <summary>Noi dang dung media — de canh bao khi xoa va hien "dang duoc su dung o...".</summary>
public class MediaUsage
{
    public Guid MediaId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
}

public enum MediaKind
{
    Image,
    Video,
    Document,
    Other,
}

public enum MediaProcessingState
{
    None,
    Pending,
    Done,
    Failed,
}
