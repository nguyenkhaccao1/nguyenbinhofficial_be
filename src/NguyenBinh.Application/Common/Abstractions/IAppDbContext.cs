using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Media;
using NguyenBinh.Domain.Platform;
using NguyenBinh.Domain.Settings;

namespace NguyenBinh.Application.Common.Abstractions;

/// <summary>
/// DbContext nhin tu Application. EF Core da la Unit of Work + Repository nen khong boc them lop repository.
/// </summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<SiteSetting> SiteSettings { get; }

    DbSet<MediaFolder> MediaFolders { get; }
    DbSet<MediaFile> MediaFiles { get; }
    DbSet<MediaUsage> MediaUsages { get; }

    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ContentVersion> ContentVersions { get; }
    DbSet<ContentTranslation> ContentTranslations { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
