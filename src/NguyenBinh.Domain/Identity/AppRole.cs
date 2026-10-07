using Microsoft.AspNetCore.Identity;
using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Identity;

public class AppRole : IdentityRole<Guid>, IAuditable
{
    public AppRole()
    {
        Id = Guid.CreateVersion7();
    }

    public AppRole(string name) : this()
    {
        Name = name;
        NormalizedName = name.ToUpperInvariant();
    }

    public string? Description { get; set; }

    /// <summary>Role he thong (SuperAdmin, Admin...) khong duoc xoa/doi ten.</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
}

/// <summary>Danh muc permission — dong bo tu NguyenBinh.Shared.Authorization.Permissions, khong sua tay.</summary>
public class Permission
{
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
