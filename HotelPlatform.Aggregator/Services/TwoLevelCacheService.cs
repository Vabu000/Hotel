namespace HotelPlatform.Aggregator.Services;

public class TwoLevelCacheService
{
    private readonly MemoryCacheService _l1Cache;
    private readonly RedisCacheService _l2Cache;
    private readonly MetricsService _metrics;
    private readonly ILogger<TwoLevelCacheService> _logger;

    public TwoLevelCacheService(
        MemoryCacheService l1Cache,
        RedisCacheService l2Cache,
        MetricsService metrics,
        ILogger<TwoLevelCacheService> logger)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? l1Expiration = null,
        TimeSpan? l2Expiration = null,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var l1Value = await _l1Cache.GetAsync<T>(key, cancellationToken);
        sw.Stop();

        if (l1Value != null)
        {
            _logger.LogInformation("L1 cache hit for key: {CacheKey}", key);
            _metrics.RecordCacheHit("L1");
            _metrics.RecordCacheOperationDuration(sw.Elapsed.TotalMilliseconds, "Get", "L1");
            return l1Value;
        }
        try
        {
            sw.Restart();
            var l2Value = await _l2Cache.GetAsync<T>(key, cancellationToken);
            sw.Stop();

            if (l2Value != null)
            {
                _logger.LogInformation("L2 cache hit for key: {CacheKey}, refreshing L1", key);
                _metrics.RecordCacheHit("L2");
                _metrics.RecordCacheOperationDuration(sw.Elapsed.TotalMilliseconds, "Get", "L2");

                sw.Restart();
                await _l1Cache.SetAsync(key, l2Value, l1Expiration ?? TimeSpan.FromMinutes(2), cancellationToken);
                sw.Stop();
                _metrics.RecordCacheOperationDuration(sw.Elapsed.TotalMilliseconds, "Set", "L1");

                return l2Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis (L2) cache unavailable for key: {CacheKey}, falling back to factory", key);
            
        }

        _logger.LogInformation("Cache miss for key: {CacheKey}, calling factory", key);
        _metrics.RecordCacheMiss();
        var value = await factory();

        if (value != null)
        {
            sw.Restart();
            await _l1Cache.SetAsync(key, value, l1Expiration ?? TimeSpan.FromMinutes(2), cancellationToken);
            sw.Stop();
            _metrics.RecordCacheOperationDuration(sw.Elapsed.TotalMilliseconds, "Set", "L1");

            try
            {
                sw.Restart();
                await _l2Cache.SetAsync(key, value, l2Expiration ?? TimeSpan.FromMinutes(10), cancellationToken);
                sw.Stop();
                _metrics.RecordCacheOperationDuration(sw.Elapsed.TotalMilliseconds, "Set", "L2");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write to Redis (L2) cache for key: {CacheKey}, continuing without L2", key);
            }
        }

        return value;
    }

    public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating cache for key: {CacheKey}", key);

        await _l1Cache.RemoveAsync(key, cancellationToken);

        try
        {
            await _l2Cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate Redis (L2) cache for key: {CacheKey}, L1 invalidated successfully", key);
        }
    }
}