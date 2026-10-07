using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Platform;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Platform;

namespace NguyenBinh.Application.Identity.Auth;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task<AuthSession> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Thu hoi moi refresh token con hieu luc cua user (khoa user, doi/reset mat khau...).</summary>
    Task RevokeAllSessionsAsync(Guid userId, string reason, CancellationToken ct = default);
}

internal sealed class AuthService(
    UserManager<AppUser> userManager,
    IAppDbContext db,
    ITokenService tokenService,
    IPermissionService permissionService,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IAuthService
{
    private const string InvalidCredentials = "Email hoặc mật khẩu không đúng.";

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            await audit.LogAsync(AuditActions.LoginFailed, "User", null, new { request.Email, reason = "UNKNOWN_EMAIL" }, ct: ct);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await audit.LogAsync(AuditActions.LoginFailed, "User", user.Id.ToString(), new { reason = "LOCKED_OUT" },
                user.Id, user.Email, ct);
            throw new UnauthorizedException("Tài khoản tạm khoá do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            await audit.LogAsync(AuditActions.LoginFailed, "User", user.Id.ToString(), new { reason = "WRONG_PASSWORD" },
                user.Id, user.Email, ct);
            throw new UnauthorizedException(InvalidCredentials);
        }

        // Kiem tra sau mat khau de khong lo tai khoan ton tai/bi khoa cho nguoi khong biet mat khau.
        if (!user.IsActive)
        {
            await audit.LogAsync(AuditActions.LoginFailed, "User", user.Id.ToString(), new { reason = "INACTIVE" },
                user.Id, user.Email, ct);
            throw new UnauthorizedException("Tài khoản đã bị vô hiệu hoá.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = clock.GetUtcNow();

        var (session, _) = await IssueSessionAsync(user, Guid.CreateVersion7(), request.RememberMe, ct);
        audit.Add(AuditActions.Login, "User", user.Id.ToString(), userId: user.Id, userName: user.Email);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new UnauthorizedException();

        var now = clock.GetUtcNow();
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) throw new UnauthorizedException();

        if (token.RevokedAt is not null)
        {
            if (token.RevokedReason == RefreshTokenRevokeReasons.Rotated)
            {
                // Token da duoc thay the ma van bi dung lai → co the da bi danh cap: huy ca family.
                await RevokeFamilyAsync(token.FamilyId, now, ct);
                audit.Add(AuditActions.TokenReuse, "User", token.UserId.ToString(), new { token.FamilyId },
                    userId: token.UserId, userName: token.User?.Email);
                await db.SaveChangesAsync(ct);
            }

            throw new UnauthorizedException();
        }

        if (token.ExpiresAt <= now) throw new UnauthorizedException("Phiên đăng nhập đã hết hạn.");

        var user = token.User;
        if (user is null || user.IsDeleted || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            token.Revoke(now, RefreshTokenRevokeReasons.UserDisabled);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException();
        }

        var (session, replacement) = await IssueSessionAsync(user, token.FamilyId, token.IsPersistent, ct);
        token.Revoke(now, RefreshTokenRevokeReasons.Rotated);
        token.ReplacedByTokenId = replacement.Id;
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return;

        token.Revoke(clock.GetUtcNow(), RefreshTokenRevokeReasons.Logout);
        audit.Add(AuditActions.Logout, "User", token.UserId.ToString(), userId: token.UserId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new UnauthorizedException();
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        return await ToDtoAsync(user, roles, ct);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();
        user.FullName = request.FullName.Trim();
        user.AvatarMediaId = request.AvatarMediaId;
        await db.SaveChangesAsync(ct);
        return await GetCurrentUserAsync(userId, ct);
    }

    public async Task<AuthSession> ChangePasswordAsync(Guid userId, ChangePasswordRequest request,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) throw result.ToValidationException("newPassword");

        // Dang xuat moi thiet bi khac, cap phien moi cho thiet bi hien tai.
        await RevokeAllSessionsAsync(userId, RefreshTokenRevokeReasons.PasswordChanged, ct);
        var (session, _) = await IssueSessionAsync(user, Guid.CreateVersion7(), false, ct);
        audit.Add(AuditActions.PasswordReset, "User", user.Id.ToString(), new { self = true });
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task RevokeAllSessionsAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(now, reason);
        await db.SaveChangesAsync(ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(now, RefreshTokenRevokeReasons.ReuseDetected);
    }

    /// <summary>Tao access token + refresh token moi (refresh token duoc Add vao DbContext, caller SaveChanges).</summary>
    private async Task<(AuthSession Session, RefreshToken Token)> IssueSessionAsync(AppUser user, Guid familyId,
        bool persistent, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var access = tokenService.CreateAccessToken(user, roles);

        var raw = GenerateRefreshToken();
        var days = persistent ? options.Value.PersistentRefreshTokenDays : options.Value.RefreshTokenDays;
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            IsPersistent = persistent,
            CreatedAt = now,
            ExpiresAt = now.AddDays(days),
            CreatedByIp = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent is { Length: > 512 } ua ? ua[..512] : currentUser.UserAgent,
        };
        db.RefreshTokens.Add(refresh);

        var session = new AuthSession(access.Token, access.ExpiresAt, raw, refresh.ExpiresAt, persistent,
            await ToDtoAsync(user, roles, ct));
        return (session, refresh);
    }

    private async Task<CurrentUserDto> ToDtoAsync(AppUser user, List<string> roles, CancellationToken ct)
    {
        var permissions = await permissionService.GetPermissionsAsync(roles, ct);
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FullName, user.AvatarMediaId, roles,
            permissions.Order(StringComparer.Ordinal).ToList());
    }

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
