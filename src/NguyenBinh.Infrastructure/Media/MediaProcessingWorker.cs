using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Media;
using NguyenBinh.Infrastructure.Persistence;

namespace NguyenBinh.Infrastructure.Media;

internal sealed class MediaProcessingQueue : IMediaProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid mediaId, CancellationToken ct = default) => _channel.Writer.WriteAsync(mediaId, ct);
}

/// <summary>
/// Tao bien the anh nen. Khi khoi dong, nap lai cac media con Pending (vd API restart giua chung).
/// </summary>
internal sealed class MediaProcessingWorker(
    MediaProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MediaProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var mediaId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IMediaVariantProcessor>().ProcessAsync(mediaId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Media processing crashed for {MediaId}", mediaId);
            }
        }
    }

    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await db.MediaFiles.Where(m => m.ProcessingState == MediaProcessingState.Pending)
                .Select(m => m.Id).ToListAsync(ct);
            foreach (var id in pending) await queue.EnqueueAsync(id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not requeue pending media");
        }
    }
}

/// <summary>Mac dinh khong quet; thay bang ClamAV khi trien khai production neu can.</summary>
internal sealed class NoopMalwareScanner : IMalwareScanner
{
    public Task<bool> IsCleanAsync(Stream content, string fileName, CancellationToken ct = default) =>
        Task.FromResult(true);
}
