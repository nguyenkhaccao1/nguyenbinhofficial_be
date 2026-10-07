using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NguyenBinh.Domain.Common;
using NguyenBinh.Infrastructure.Persistence;

namespace NguyenBinh.Infrastructure.Content;

/// <summary>
/// Moi phut: noi dung Scheduled da toi gio → Published. Website da coi Scheduled-toi-gio la public
/// (ContentEntity.IsPublicAt), job nay chi dong bo trang thai hien thi trong admin.
/// </summary>
internal sealed class ScheduledPublishWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ScheduledPublishWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await PublishDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Scheduled publish run failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishDueAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();

        var contentTypes = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && typeof(ContentEntity).IsAssignableFrom(t.ClrType) && !t.ClrType.IsAbstract)
            .Select(t => t.ClrType);

        foreach (var type in contentTypes)
        {
            var updated = await (Task<int>)typeof(ScheduledPublishWorker)
                .GetMethod(nameof(PublishDue), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(type)
                .Invoke(null, [db, now, ct])!;
            if (updated > 0) logger.LogInformation("Published {Count} scheduled {Type}", updated, type.Name);
        }
    }

    private static Task<int> PublishDue<T>(AppDbContext db, DateTimeOffset now, CancellationToken ct) where T : ContentEntity =>
        db.Set<T>()
            .Where(e => e.Status == ContentStatus.Scheduled && e.PublishAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, ContentStatus.Published)
                .SetProperty(e => e.PublishedAt, e => e.PublishedAt ?? now)
                .SetProperty(e => e.PublishAt, (DateTimeOffset?)null), ct);
}
