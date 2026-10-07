using Microsoft.EntityFrameworkCore;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Application.Media;

public sealed record MediaUsageRef(Guid MediaId, string Field);

/// <summary>
/// Ghi lai entity nao dang dung media nao (goi moi lan luu entity) — de canh bao khi xoa media
/// va hien thi "dang duoc su dung o...". Chi thay doi DbContext, caller goi SaveChanges.
/// </summary>
public interface IMediaUsageTracker
{
    Task SetUsagesAsync(string entityType, string entityId, IEnumerable<MediaUsageRef> usages,
        CancellationToken ct = default);
}

internal sealed class MediaUsageTracker(IAppDbContext db) : IMediaUsageTracker
{
    public async Task SetUsagesAsync(string entityType, string entityId, IEnumerable<MediaUsageRef> usages,
        CancellationToken ct = default)
    {
        var desired = usages.Distinct().ToList();
        var existing = await db.MediaUsages
            .Where(u => u.EntityType == entityType && u.EntityId == entityId)
            .ToListAsync(ct);

        foreach (var stale in existing.Where(e => !desired.Any(d => d.MediaId == e.MediaId && d.Field == e.Field)))
            db.MediaUsages.Remove(stale);

        foreach (var add in desired.Where(d => !existing.Any(e => e.MediaId == d.MediaId && e.Field == d.Field)))
            db.MediaUsages.Add(new MediaUsage
            {
                MediaId = add.MediaId, EntityType = entityType, EntityId = entityId, Field = add.Field,
            });
    }
}
