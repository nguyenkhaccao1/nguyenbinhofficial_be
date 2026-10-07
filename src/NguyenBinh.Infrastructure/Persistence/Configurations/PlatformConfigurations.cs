using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NguyenBinh.Domain.Media;
using NguyenBinh.Domain.Platform;
using NguyenBinh.Domain.Settings;

namespace NguyenBinh.Infrastructure.Persistence.Configurations;

internal sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> b)
    {
        b.ToTable("SiteSettings", t => t.HasCheckConstraint("CK_SiteSettings_ValueJson", "ISJSON([ValueJson]) = 1"));
        b.Property(x => x.Key).HasMaxLength(64);
        b.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class MediaFolderConfiguration : IEntityTypeConfiguration<MediaFolder>
{
    public void Configure(EntityTypeBuilder<MediaFolder> b)
    {
        b.ToTable("MediaFolders");
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ParentId, x.Name });
    }
}

internal sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> b)
    {
        b.ToTable("MediaFiles");
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.OriginalName).HasMaxLength(255);
        b.Property(x => x.StorageKey).HasMaxLength(400).IsUnicode(false);
        b.Property(x => x.StorageFileId).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.MimeType).HasMaxLength(150).IsUnicode(false);
        b.Property(x => x.Extension).HasMaxLength(16).IsUnicode(false);
        b.Property(x => x.Title).HasMaxLength(255);
        b.Property(x => x.Alt).HasMaxLength(300);
        b.Property(x => x.Caption).HasMaxLength(1000);
        b.Property(x => x.Checksum).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.BlurDataUrl).HasMaxLength(2000).IsUnicode(false);
        b.Property(x => x.Tags); // primitive collection → JSON
        b.OwnsMany(x => x.Variants, v => v.ToJson());
        b.HasOne(x => x.Folder).WithMany().HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.HasIndex(x => new { x.FolderId, x.CreatedAt });
        b.HasIndex(x => x.Checksum);
    }
}

internal sealed class MediaUsageConfiguration : IEntityTypeConfiguration<MediaUsage>
{
    public void Configure(EntityTypeBuilder<MediaUsage> b)
    {
        b.ToTable("MediaUsages");
        b.HasKey(x => new { x.MediaId, x.EntityType, x.EntityId, x.Field });
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Field).HasMaxLength(100);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasOne<MediaFile>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.UserName).HasMaxLength(256);
        b.Property(x => x.Action).HasMaxLength(32);
        b.Property(x => x.EntityType).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.ChangedColumns).HasMaxLength(2000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> b)
    {
        b.ToTable("ContentVersions", t => t.HasCheckConstraint("CK_ContentVersions_Snapshot", "ISJSON([SnapshotJson]) = 1"));
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Version }).IsUnique();
    }
}

internal sealed class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> b)
    {
        b.ToTable("ContentTranslations");
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.Locale).HasMaxLength(10);
        b.Property(x => x.Field).HasMaxLength(100);
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Locale, x.Field }).IsUnique();
    }
}
