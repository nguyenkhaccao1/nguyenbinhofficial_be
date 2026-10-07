using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Identity;

/// <summary>
/// Refresh token xoay vong. Chi luu SHA-256 cua token. Cac token sinh ra tu cung 1 lan dang nhap
/// chung FamilyId: phat hien dung lai token da bi thay the → thu hoi ca family.
/// </summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public bool IsPersistent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public AppUser? User { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        RevokedReason = reason;
    }
}

public static class RefreshTokenRevokeReasons
{
    public const string Rotated = "ROTATED";
    public const string Logout = "LOGOUT";
    public const string ReuseDetected = "REUSE_DETECTED";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string UserDisabled = "USER_DISABLED";
}
