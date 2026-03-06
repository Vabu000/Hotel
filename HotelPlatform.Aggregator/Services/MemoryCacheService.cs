using Microsoft.Extensions.Caching.Memory;

namespace HotelPlatform.Aggregator.Services;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<T>(key, out var value))
        {
            _logger.LogInformation("Memory cache HIT: {CacheKey}", key);
            return Task.FromResult<T?>(value);
        }

        _logger.LogInformation("Memory cache MISS: {CacheKey}", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5),
            Size = 1
        };

        _cache.Set(key, value, options);
        _logger.LogInformation("Memory cache SET: {CacheKey}, Expiration: {Expiration}", key, expiration);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        _logger.LogInformation("Memory cache REMOVE: {CacheKey}", key);
        return Task.CompletedTask;
    }
}