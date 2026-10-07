using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Identity;
using NguyenBinh.Domain.Platform;

namespace NguyenBinh.Infrastructure.Persistence;

/// <summary>
/// Sinh AuditLog tu ChangeTracker cho entity nghiep vu (AuditableEntity, User, Role):
/// chi ghi cot thay doi, bo qua cot nhay cam/ky thuat.
/// </summary>
internal static class AuditTrail
{
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(AppUser.PasswordHash), nameof(AppUser.SecurityStamp), nameof(AppUser.ConcurrencyStamp),
        nameof(ContentEntity.RowVersion),
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy), nameof(IAuditable.UpdatedAt),
        nameof(IAuditable.UpdatedBy), nameof(ISoftDeletable.DeletedAt), nameof(ISoftDeletable.DeletedBy),
        // Media: ket qua xu ly anh nen, khong phai thao tac cua nguoi dung.
        "Variants", "ProcessingState", "BlurDataUrl",
        // Thay doi ky thuat khi dang nhap.
        nameof(AppUser.AccessFailedCount), nameof(AppUser.LockoutEnd), nameof(AppUser.LastLoginAt),
    ];

    private const int MaxValueLength = 4000;

    public static IEnumerable<AuditLog> Build(ChangeTracker tracker, ICurrentUser user, DateTimeOffset now)
    {
        var logs = new List<AuditLog>();
        foreach (var entry in tracker.Entries())
        {
            if (!IsAudited(entry) || entry.State is EntityState.Detached or EntityState.Unchanged) continue;

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            var changed = new List<string>();

            foreach (var prop in entry.Properties)
            {
                var name = prop.Metadata.Name;
                if (IgnoredProperties.Contains(name) || prop.Metadata.IsPrimaryKey()) continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        if (prop.CurrentValue is not null) newValues[name] = Trim(prop.CurrentValue);
                        break;
                    case EntityState.Deleted:
                        oldValues[name] = Trim(prop.OriginalValue);
                        break;
                    case EntityState.Modified when prop.IsModified &&
                                                  !prop.Metadata.GetValueComparer().Equals(prop.OriginalValue, prop.CurrentValue):
                        oldValues[name] = Trim(prop.OriginalValue);
                        newValues[name] = Trim(prop.CurrentValue);
                        changed.Add(name);
                        break;
                }
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditActions.Create,
                EntityState.Deleted => AuditActions.Delete,
                _ when changed.Contains(nameof(ISoftDeletable.IsDeleted)) =>
                    entry.Entity is ISoftDeletable { IsDeleted: true } ? AuditActions.Delete : AuditActions.Restore,
                _ => AuditActions.Update,
            };

            if (action == AuditActions.Update && changed.Count == 0) continue;

            logs.Add(new AuditLog
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues),
                ChangedColumns = changed.Count == 0 ? null : string.Join(',', changed),
                IpAddress = user.IpAddress,
                UserAgent = user.UserAgent is { Length: > 512 } ua ? ua[..512] : user.UserAgent,
                CorrelationId = user.CorrelationId,
                CreatedAt = now,
            });
        }

        return logs;
    }

    private static bool IsAudited(EntityEntry entry) =>
        !entry.Metadata.IsOwned() && entry.Entity is AuditableEntity or AppUser or AppRole;

    private static object? Trim(object? value) =>
        value is string { Length: > MaxValueLength } s ? s[..MaxValueLength] + "…" : value;
}
