namespace NguyenBinh.Domain.Common;

public abstract class Entity
{
    /// <summary>Guid v7 tang dan theo thoi gian: khong lo so luong, khong phan manh clustered index.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>Bang co audit + soft delete (dien tu dong khi SaveChanges).</summary>
public abstract class AuditableEntity : Entity, IAuditable, ISoftDeletable
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Noi dung co vong doi xuat ban (Draft/Scheduled/Published...) va kiem soat ghi dong thoi.</summary>
public abstract class ContentEntity : AuditableEntity
{
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// Noi dung hien thi public khi da Published, hoac Scheduled va da toi gio —
    /// khong phu thuoc job chuyen trang thai chay dung gio.
    /// </summary>
    public bool IsPublicAt(DateTimeOffset now) =>
        !IsDeleted && (Status == ContentStatus.Published
                       || (Status == ContentStatus.Scheduled && PublishAt <= now));
}

public enum ContentStatus
{
    Draft,
    Scheduled,
    Published,
    Unpublished,
    Archived,
}
