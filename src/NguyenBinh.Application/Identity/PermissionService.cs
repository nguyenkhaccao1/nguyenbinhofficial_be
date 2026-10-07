using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Shared.Authorization;

namespace NguyenBinh.Application.Identity;

/// <summary>
/// Tra permission theo role. Token chi mang role, khong mang permission → doi quyen role co hieu luc
/// ngay o request ke tiep (sau khi cache bi xoa).
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roleNames, CancellationToken ct = default);
    Task InvalidateRoleAsync(string roleName, CancellationToken ct = default);
}

internal sealed class PermissionService(IAppDbContext db, ICacheService cache) : IPermissionService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(IEnumerable<string> roleNames,
        CancellationToken ct = default)
    {
        var roles = roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (roles.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
            return Permissions.AllCodes.ToHashSet();

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            var codes = await cache.GetOrCreateAsync(CacheKey(role), async token =>
            {
                var normalized = role.ToUpperInvariant();
                return await (from r in db.Roles
                              join rp in db.RolePermissions on r.Id equals rp.RoleId
                              where r.NormalizedName == normalized
                              select rp.PermissionCode).ToListAsync(token);
            }, Ttl, ct);
            result.UnionWith(codes);
        }

        return result;
    }

    public Task InvalidateRoleAsync(string roleName, CancellationToken ct = default) =>
        cache.RemoveAsync(CacheKey(roleName), ct);

    private static string CacheKey(string role) => $"perm:role:{role.ToUpperInvariant()}";
}
