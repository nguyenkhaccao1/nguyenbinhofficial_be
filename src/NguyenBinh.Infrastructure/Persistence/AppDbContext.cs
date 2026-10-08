using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Media;
using NguyenBinh.Domain.Platform;
using NguyenBinh.Domain.Settings;

namespace NguyenBinh.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : IdentityDbContext<AppUser, AppRole, Guid, IdentityUserClaim<Guid>, IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options), IAppDbContext
{
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<MediaFolder> MediaFolders => Set<MediaFolder>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<MediaUsage> MediaUsages => Set<MediaUsage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<ContentTranslation> ContentTranslations => Set<ContentTranslation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyContentConventions(builder);
        ApplySoftDeleteFilters(builder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enum luu dang chuoi: doc DB de hieu, them gia tri moi khong lam lech du lieu cu.
        builder.Properties<ContentStatus>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<MediaKind>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<MediaProcessingState>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<ProjectContentType>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<CommercialType>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<ProjectRole>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<OwnershipType>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<ProjectState>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<ProjectMediaKind>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<ProjectLinkKind>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<ProductType>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<BillingPeriod>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<TechnologyGroup>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<FaqScope>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<PageType>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<Domain.Leads.LeadFormType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<Domain.Leads.LeadStatus>().HaveConversion<string>().HaveMaxLength(20);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BeforeSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        BeforeSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Dien cot audit, doi Delete thanh soft delete, ghi AuditLogs trong cung transaction.</summary>
    private void BeforeSave()
    {
        ChangeTracker.DetectChanges();
        var now = clock.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy ??= userId;
                }
                else if (entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                }
            }

            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable })
                SoftDelete(entry, now, userId);
        }

        AuditLogs.AddRange(AuditTrail.Build(ChangeTracker, currentUser, now));
    }

    /// <summary>
    /// Doi Delete thanh cap nhat co IsDeleted. Chi danh dau cac cot soft delete la modified, va tra
    /// cac entity owned (vd Variants luu JSON) ve Unchanged — neu khong EF se coi khoa cua chung bi sua.
    /// </summary>
    private void SoftDelete(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, DateTimeOffset now,
        Guid? userId)
    {
        entry.State = EntityState.Unchanged;
        foreach (var navigation in entry.Navigations.Where(n => n.Metadata.TargetEntityType.IsOwned()))
        {
            var owned = navigation.CurrentValue switch
            {
                null => [],
                System.Collections.IEnumerable items and not string => items.Cast<object>(),
                var single => [single],
            };
            foreach (var item in owned) Entry(item).State = EntityState.Unchanged;
        }

        entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
        entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
        entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = userId;
        if (entry.Entity is IAuditable)
        {
            entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
            entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = userId;
        }

        // Cac cot khac da duoc doi truoc khi Remove (vd UserService doi email) van phai duoc luu.
        foreach (var prop in entry.Properties.Where(p => !p.Metadata.IsPrimaryKey()))
            if (!prop.Metadata.GetValueComparer().Equals(prop.OriginalValue, prop.CurrentValue))
                prop.IsModified = true;
    }

    /// <summary>RowVersion cho moi noi dung (409 khi sua dong thoi) va SEO luu JSON tren bang cua entity.</summary>
    private static void ApplyContentConventions(ModelBuilder builder)
    {
        foreach (var type in builder.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList())
        {
            // Id (Guid v7) do ung dung sinh → bao EF khong cho DB sinh. Neu khong, ban ghi con moi them vao
            // collection (features, media...) khi sua bi EF coi la ban ghi cu va UPDATE 0 dong → loi concurrency.
            if (typeof(Entity).IsAssignableFrom(type.ClrType) && type.BaseType is null)
                builder.Entity(type.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();

            if (typeof(ContentEntity).IsAssignableFrom(type.ClrType))
                builder.Entity(type.ClrType).Property(nameof(ContentEntity.RowVersion)).IsRowVersion();
            if (typeof(IHasSeo).IsAssignableFrom(type.ClrType))
                builder.Entity(type.ClrType).OwnsOne(typeof(SeoMeta), nameof(IHasSeo.Seo), seo => seo.ToJson());
            if (typeof(ContentEntity).IsAssignableFrom(type.ClrType))
                builder.Entity(type.ClrType).HasIndex(nameof(ContentEntity.Status));
        }
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var type in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(type.ClrType) || type.BaseType is not null) continue;

            var parameter = Expression.Parameter(type.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
            type.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
