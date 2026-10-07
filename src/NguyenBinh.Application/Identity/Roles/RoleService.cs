using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Platform;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Platform;
using NguyenBinh.Shared.Authorization;

namespace NguyenBinh.Application.Identity.Roles;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<string> Permissions);

public sealed record PermissionDto(string Code, string Module, string Action);

public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record UpdateRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

internal sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên vai trò.").MaximumLength(64)
            .Matches("^[A-Za-z][A-Za-z0-9_-]*$").WithMessage("Tên vai trò chỉ gồm chữ không dấu, số, '-' và '_'.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.Permissions).Must(Permissions.Exists).WithMessage("Quyền '{PropertyValue}' không tồn tại.");
    }
}

internal sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên vai trò.").MaximumLength(64)
            .Matches("^[A-Za-z][A-Za-z0-9_-]*$").WithMessage("Tên vai trò chỉ gồm chữ không dấu, số, '-' và '_'.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleForEach(x => x.Permissions).Must(Permissions.Exists).WithMessage("Quyền '{PropertyValue}' không tồn tại.");
    }
}

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default);
    Task<RoleDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    IReadOnlyList<PermissionDto> ListPermissions();
}

internal sealed class RoleService(
    RoleManager<AppRole> roleManager,
    IAppDbContext db,
    IPermissionService permissionService,
    IAuditLogger audit,
    ICurrentUser currentUser) : IRoleService
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default)
    {
        var roles = await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id, r.Name, r.Description, r.IsSystem,
                UserCount = db.UserRoles.Count(ur => ur.RoleId == r.Id &&
                                                     db.Users.Any(u => u.Id == ur.UserId)),
                Permissions = r.Permissions.Select(p => p.PermissionCode).ToList(),
            })
            .ToListAsync(ct);

        // Role he thong theo thu tu cap bac (SuperAdmin → Viewer), role tu tao xep sau theo ten.
        var hierarchy = SystemRoles.All.ToList();
        return roles
            .OrderBy(r => hierarchy.IndexOf(r.Name!) is var i and >= 0 ? i : int.MaxValue)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name!, r.Description, r.IsSystem, r.UserCount,
                EffectivePermissions(r.Name!, r.Permissions))).ToList();
    }

    public async Task<RoleDto> GetAsync(Guid id, CancellationToken ct = default) =>
        (await ListAsync(ct)).FirstOrDefault(r => r.Id == id) ?? throw NotFoundException.For("vai trò", id);

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        await EnsureCanGrantAsync(request.Permissions, ct);

        var role = new AppRole(request.Name.Trim()) { Description = request.Description?.Trim() };
        foreach (var code in request.Permissions.Distinct())
            role.Permissions.Add(new RolePermission { PermissionCode = code });

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded) throw result.ToValidationException("name");

        await audit.LogAsync(AuditActions.PermissionChange, "Role", role.Id.ToString(),
            new { role.Name, permissions = request.Permissions }, ct: ct);
        return await GetAsync(role.Id, ct);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw NotFoundException.For("vai trò", id);

        if (role.Name == SystemRoles.SuperAdmin)
            throw new ConflictException("Không thể chỉnh sửa vai trò SuperAdmin (luôn có toàn quyền).");
        if (role.IsSystem && !string.Equals(role.Name, request.Name.Trim(), StringComparison.Ordinal))
            throw new BusinessValidationException("name", "Không thể đổi tên vai trò hệ thống.");
        var oldName = role.Name!;
        var oldPermissions = role.Permissions.Select(p => p.PermissionCode).Order().ToList();
        var newPermissions = request.Permissions.Distinct().Order().ToList();
        await EnsureCanGrantAsync(newPermissions.Except(oldPermissions), ct);

        if (!role.IsSystem && oldName != request.Name.Trim())
        {
            var renamed = await roleManager.SetRoleNameAsync(role, request.Name.Trim());
            if (!renamed.Succeeded) throw renamed.ToValidationException("name");
        }

        role.Description = request.Description?.Trim();
        role.Permissions.Clear();
        foreach (var code in newPermissions) role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded) throw result.ToValidationException("name");

        audit.Add(AuditActions.PermissionChange, "Role", role.Id.ToString(),
            new { role.Name, permissions = newPermissions }, new { name = oldName, permissions = oldPermissions });
        await db.SaveChangesAsync(ct);

        await permissionService.InvalidateRoleAsync(oldName, ct);
        await permissionService.InvalidateRoleAsync(role.Name!, ct);
        return await GetAsync(role.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw NotFoundException.For("vai trò", id);
        if (role.IsSystem) throw new ConflictException("Không thể xoá vai trò hệ thống.");
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct))
            throw new ConflictException("Vai trò đang được gán cho người dùng. Gỡ vai trò khỏi người dùng trước khi xoá.");

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded) throw result.ToValidationException("name");

        await audit.LogAsync(AuditActions.Delete, "Role", role.Id.ToString(), new { role.Name }, ct: ct);
        await permissionService.InvalidateRoleAsync(role.Name!, ct);
    }

    public IReadOnlyList<PermissionDto> ListPermissions() =>
        Permissions.All.Select(p => new PermissionDto(p.Code, p.Module, p.Action)).ToList();

    /// <summary>Chong leo thang quyen: chi duoc cap nhung quyen ma chinh minh dang co (SuperAdmin co tat ca).</summary>
    private async Task EnsureCanGrantAsync(IEnumerable<string> granting, CancellationToken ct)
    {
        var own = await permissionService.GetPermissionsAsync(currentUser.Roles, ct);
        var notOwned = granting.Where(p => !own.Contains(p)).ToList();
        if (notOwned.Count > 0)
            throw new ForbiddenException($"Bạn không thể cấp quyền mà mình không có: {string.Join(", ", notOwned)}.");
    }

    private static IReadOnlyList<string> EffectivePermissions(string roleName, List<string> stored) =>
        roleName == SystemRoles.SuperAdmin ? Permissions.AllCodes.ToList() : stored.Order().ToList();
}
