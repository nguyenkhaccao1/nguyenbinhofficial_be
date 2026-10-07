using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Platform;

public class AuditLog : Entity
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? ChangedColumns { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class AuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Restore = "RESTORE";
    public const string Publish = "PUBLISH";
    public const string Unpublish = "UNPUBLISH";
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string TokenReuse = "TOKEN_REUSE";
    public const string PermissionChange = "PERMISSION_CHANGE";
    public const string PasswordReset = "PASSWORD_RESET";
}

/// <summary>Snapshot day du cua 1 aggregate de xem/khoi phuc phien ban (va autosave).</summary>
public class ContentVersion : Entity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public bool IsAutosave { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Ban dich theo truong (vi la mac dinh trong cot chinh; en... nam o day).</summary>
public class ContentTranslation : Entity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
