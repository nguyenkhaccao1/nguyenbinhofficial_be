using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Public;
using NguyenBinh.Domain.Common;
using NguyenBinh.Domain.Content;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.Infrastructure.Persistence;

/// <summary>
/// Sau khi luu thanh cong thay doi noi dung (du an, trang, menu, media...), lam moi cache public de website
/// hien noi dung moi ngay — admin xuat ban xong la thay, khong phai cho TTL.
/// </summary>
public sealed class PublicCacheInvalidator(ICacheService cache) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, object> _dirty = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && context.ChangeTracker.Entries().Any(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is ContentEntity or Menu or MenuItem or MediaFile))
            _dirty.AddOrUpdate(context, true);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _dirty.Remove(context))
            await cache.RemoveAsync(PublicCache.VersionKey, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
