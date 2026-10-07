using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Application.Identity.Auth;
using NguyenBinh.Application.Platform;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Platform;
using NguyenBinh.Shared.Authorization;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Application.Identity.Users;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken ct = default);
    Task<UserDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDetailDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task LockAsync(Guid id, CancellationToken ct = default);
    Task UnlockAsync(Guid id, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);
}

internal sealed class UserService(
    UserManager<AppUser> userManager,
    IAppDbContext db,
    IAuthService authService,
    IAuditLogger audit,
    ICurrentUser currentUser,
    TimeProvider clock) : IUserService
{
    private static readonly SortMap<AppUser> Sorts = new SortMap<AppUser>()
        .Add("email", u => u.Email)
        .Add("fullName", u => u.FullName)
        .Add("lastLoginAt", u => u.LastLoginAt)
        .Add("createdAt", u => u.CreatedAt);

    public async Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var users = db.Users.AsNoTracking();

        if (query.Search is { } q)
            users = users.Where(u => u.Email!.Contains(q) || u.FullName.Contains(q));
        if (query.IsActive is { } active)
            users = users.Where(u => u.IsActive == active);
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var normalized = query.Role.ToUpperInvariant();
            users = users.Where(u => db.UserRoles.Any(ur =>
                ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == normalized)));
        }

        var page = await Sorts.Apply(users, query.Sort, "-createdAt").ToPagedAsync(query, ct);
        var roles = await RolesByUserAsync(page.Items.Select(u => u.Id), ct);
        var now = clock.GetUtcNow();

        return page.Map(u => new UserListItemDto(u.Id, u.Email ?? string.Empty, u.FullName,
            roles.GetValueOrDefault(u.Id, []), u.IsActive, u.LockoutEnd > now, u.LastLoginAt, u.CreatedAt));
    }

    public async Task<UserDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        return await ToDetailAsync(user);
    }

    public async Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var roles = await ValidateRolesAsync(request.Roles, ct);
        EnsureCanAssign(roles);

        var user = new AppUser
        {
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            IsActive = request.IsActive,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) throw result.ToValidationException("password");

        result = await userManager.AddToRolesAsync(user, roles);
        if (!result.Succeeded) throw result.ToValidationException("roles");

        await audit.LogAsync(AuditActions.PermissionChange, "User", user.Id.ToString(), new { roles }, ct: ct);
        return await ToDetailAsync(user);
    }

    public async Task<UserDetailDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        var currentRoles = (await userManager.GetRolesAsync(user)).ToList();
        EnsureCanManage(currentRoles);

        var roles = await ValidateRolesAsync(request.Roles, ct);
        EnsureCanAssign(roles);

        var isSelf = user.Id == currentUser.UserId;
        if (isSelf && !request.IsActive)
            throw new BusinessValidationException("isActive", "Không thể tự vô hiệu hoá tài khoản của mình.");

        var removingSuperAdmin = currentRoles.Contains(SystemRoles.SuperAdmin) &&
                                 (!roles.Contains(SystemRoles.SuperAdmin) || !request.IsActive);
        if (removingSuperAdmin) await EnsureNotLastSuperAdminAsync(user.Id, ct);

        var wasActive = user.IsActive;
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.IsActive = request.IsActive;

        var toRemove = currentRoles.Except(roles).ToList();
        var toAdd = roles.Except(currentRoles).ToList();
        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);

        // Doi role/khoa user: cap nhat security stamp + thu hoi refresh token de phien cu het hieu luc.
        if (toRemove.Count > 0 || toAdd.Count > 0 || (wasActive && !user.IsActive))
        {
            await userManager.UpdateSecurityStampAsync(user);
            await authService.RevokeAllSessionsAsync(user.Id, RefreshTokenRevokeReasons.UserDisabled, ct);
            audit.Add(AuditActions.PermissionChange, "User", user.Id.ToString(),
                new { roles, user.IsActive }, new { roles = currentRoles, isActive = wasActive });
        }

        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(user);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        if (user.Id == currentUser.UserId)
            throw new ConflictException("Không thể tự xoá tài khoản của mình.");

        var roles = await userManager.GetRolesAsync(user);
        EnsureCanManage(roles);
        if (roles.Contains(SystemRoles.SuperAdmin)) await EnsureNotLastSuperAdminAsync(user.Id, ct);

        await authService.RevokeAllSessionsAsync(user.Id, RefreshTokenRevokeReasons.UserDisabled, ct);

        // Giai phong email/username de co the tao lai tai khoan cung email; ban goc nam trong audit log.
        var marker = $"deleted.{user.Id:N}.";
        user.UserName = marker + user.UserName;
        user.NormalizedUserName = marker.ToUpperInvariant() + user.NormalizedUserName;
        user.Email = marker + user.Email;
        user.NormalizedEmail = marker.ToUpperInvariant() + user.NormalizedEmail;
        user.IsActive = false;
        db.Users.Remove(user); // soft delete qua DbContext
        await db.SaveChangesAsync(ct);
    }

    public async Task LockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        if (user.Id == currentUser.UserId) throw new ConflictException("Không thể tự khoá tài khoản của mình.");

        var roles = await userManager.GetRolesAsync(user);
        EnsureCanManage(roles);
        if (roles.Contains(SystemRoles.SuperAdmin)) await EnsureNotLastSuperAdminAsync(user.Id, ct);

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await authService.RevokeAllSessionsAsync(user.Id, RefreshTokenRevokeReasons.UserDisabled, ct);
    }

    public async Task UnlockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        EnsureCanManage(await userManager.GetRolesAsync(user));
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        EnsureCanManage(await userManager.GetRolesAsync(user));

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded) throw result.ToValidationException("newPassword");

        await authService.RevokeAllSessionsAsync(user.Id, RefreshTokenRevokeReasons.PasswordChanged, ct);
        await audit.LogAsync(AuditActions.PasswordReset, "User", user.Id.ToString(), ct: ct);
    }

    private async Task<AppUser> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFoundException.For("người dùng", id);

    private async Task<List<string>> ValidateRolesAsync(IEnumerable<string> requested, CancellationToken ct)
    {
        var names = requested.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalized = names.Select(n => n.ToUpperInvariant()).ToList();
        var existing = await db.Roles.Where(r => normalized.Contains(r.NormalizedName!)).Select(r => r.Name!)
            .ToListAsync(ct);

        var missing = names.Where(n => !existing.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
            throw new BusinessValidationException("roles", $"Vai trò không tồn tại: {string.Join(", ", missing)}.");

        return existing;
    }

    /// <summary>Chi SuperAdmin duoc gan role SuperAdmin.</summary>
    private void EnsureCanAssign(IEnumerable<string> roles)
    {
        if (roles.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase) &&
            !currentUser.IsInRole(SystemRoles.SuperAdmin))
            throw new ForbiddenException("Chỉ SuperAdmin mới được gán vai trò SuperAdmin.");
    }

    /// <summary>Chi SuperAdmin duoc sua/khoa/xoa tai khoan SuperAdmin.</summary>
    private void EnsureCanManage(IEnumerable<string> targetRoles)
    {
        if (targetRoles.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase) &&
            !currentUser.IsInRole(SystemRoles.SuperAdmin))
            throw new ForbiddenException("Chỉ SuperAdmin mới được thay đổi tài khoản SuperAdmin.");
    }

    private async Task EnsureNotLastSuperAdminAsync(Guid excludingUserId, CancellationToken ct)
    {
        var normalized = SystemRoles.SuperAdmin.ToUpperInvariant();
        var others = await (from ur in db.UserRoles
                            join r in db.Roles on ur.RoleId equals r.Id
                            join u in db.Users on ur.UserId equals u.Id
                            where r.NormalizedName == normalized && u.Id != excludingUserId && u.IsActive
                            select u.Id).AnyAsync(ct);
        if (!others) throw new ConflictException("Phải còn ít nhất 1 tài khoản SuperAdmin đang hoạt động.");
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> RolesByUserAsync(IEnumerable<Guid> userIds,
        CancellationToken ct)
    {
        var ids = userIds.ToList();
        var pairs = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Name!).Order().ToList());
    }

    private async Task<UserDetailDto> ToDetailAsync(AppUser user)
    {
        var roles = (await userManager.GetRolesAsync(user)).Order().ToList();
        var now = clock.GetUtcNow();
        return new UserDetailDto(user.Id, user.Email ?? string.Empty, user.FullName, user.PhoneNumber,
            user.AvatarMediaId, roles, user.IsActive, user.LockoutEnd > now, user.LockoutEnd, user.LastLoginAt,
            user.CreatedAt, user.UpdatedAt);
    }
}
