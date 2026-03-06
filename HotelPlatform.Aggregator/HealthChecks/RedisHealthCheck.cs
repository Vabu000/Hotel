using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace HotelPlatform.Aggregator.HealthChecks;
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();

            var pingTime = await database.PingAsync();

            if (pingTime.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("Redis is responding slowly: {PingTime}ms", pingTime.TotalMilliseconds);
                return HealthCheckResult.Degraded(
                    $"Redis (L2 cache) is slow: {pingTime.TotalMilliseconds:F2}ms",
                    data: new Dictionary<string, object>
                    {
                        ["ping_ms"] = pingTime.TotalMilliseconds,
                        ["status"] = "slow"
                    });
            }

            // Redis is healthy
            var endpoints = _redis.GetEndPoints();
            var serverInfo = endpoints.Select(e => _redis.GetServer(e))
                .Where(s => s.IsConnected)
                .Select(s => new
                {
                    s.EndPoint,
                    s.IsConnected,
                    s.ServerType
                })
                .ToList();

            return HealthCheckResult.Healthy(
                $"Redis (L2 cache) is healthy: {pingTime.TotalMilliseconds:F2}ms",
                data: new Dictionary<string, object>
                {
                    ["ping_ms"] = pingTime.TotalMilliseconds,
                    ["connected_endpoints"] = serverInfo.Count,
                    ["status"] = "healthy"
                });
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed - application will continue with L1 cache only");

            return HealthCheckResult.Degraded(
                "Redis (L2 cache) is unavailable - using L1 (memory) cache only",
                ex,
                data: new Dictionary<string, object>
                {
                    ["status"] = "unavailable",
                    ["fallback"] = "L1_cache_only"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed unexpectedly");

            return HealthCheckResult.Degraded(
                $"Redis (L2 cache) check failed: {ex.Message}",
                ex,
                data: new Dictionary<string, object>
                {
                    ["status"] = "error",
                    ["fallback"] = "L1_cache_only"
                });
        }
    }
}
