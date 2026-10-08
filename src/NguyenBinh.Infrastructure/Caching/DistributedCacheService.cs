using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Abstractions;

namespace NguyenBinh.Infrastructure.Caching;

/// <summary>
/// Cache JSON tren IDistributedCache. Loi cache (Redis mat ket noi) khong lam hong request:
/// ghi log va goi thang factory.
/// </summary>
internal sealed class DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger)
    : ICacheService
{
    // Cung quy uoc voi API (enum UPPER_SNAKE): du lieu kieu object (vd block Resolved) doc lai tu cache van dung dinh dang.
    private static readonly JsonSerializerOptions Json = NguyenBinh.Application.Content.Common.ContentJson.Options;
    private const string Prefix = "nb:";

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl,
        CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetAsync(Prefix + key, ct);
            if (cached is not null) return JsonSerializer.Deserialize<T>(cached, Json)!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}", key);
        }

        var value = await factory(ct);

        try
        {
            await cache.SetAsync(Prefix + key, JsonSerializer.SerializeToUtf8Bytes(value, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(Prefix + key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache remove failed for {Key}", key);
        }
    }
}
