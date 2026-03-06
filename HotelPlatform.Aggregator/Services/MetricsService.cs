using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HotelPlatform.Aggregator.Services;
public class MetricsService
{
    private readonly Meter _meter;

    private readonly Counter<long> _roomViewsCounter;
    private readonly Counter<long> _cacheHitsCounter;
    private readonly Counter<long> _cacheMissesCounter;
    private readonly Counter<long> _grpcCallsCounter;
    private readonly Counter<long> _grpcErrorsCounter;

    private readonly Histogram<double> _aggregationDurationHistogram;
    private readonly Histogram<double> _grpcCallDurationHistogram;
    private readonly Histogram<double> _cacheOperationDurationHistogram;

    private long _activeRequests = 0;
    private readonly object _activeRequestsLock = new();

    public MetricsService(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("HotelPlatform.Aggregator");

        _roomViewsCounter = _meter.CreateCounter<long>(
            name: "hotel.aggregator.room.views",
            unit: "views",
            description: "Total number of room detail requests");

        _cacheHitsCounter = _meter.CreateCounter<long>(
            name: "hotel.aggregator.cache.hits",
            unit: "hits",
            description: "Number of cache hits");

        _cacheMissesCounter = _meter.CreateCounter<long>(
            name: "hotel.aggregator.cache.misses",
            unit: "misses",
            description: "Number of cache misses");

        _grpcCallsCounter = _meter.CreateCounter<long>(
            name: "hotel.aggregator.grpc.calls",
            unit: "calls",
            description: "Total number of gRPC calls to downstream services");

        _grpcErrorsCounter = _meter.CreateCounter<long>(
            name: "hotel.aggregator.grpc.errors",
            unit: "errors",
            description: "Number of gRPC call failures");

        _aggregationDurationHistogram = _meter.CreateHistogram<double>(
            name: "hotel.aggregator.aggregation.duration",
            unit: "ms",
            description: "Duration of room data aggregation");

        _grpcCallDurationHistogram = _meter.CreateHistogram<double>(
            name: "hotel.aggregator.grpc.duration",
            unit: "ms",
            description: "Duration of individual gRPC calls");

        _cacheOperationDurationHistogram = _meter.CreateHistogram<double>(
            name: "hotel.aggregator.cache.duration",
            unit: "ms",
            description: "Duration of cache operations");

        _meter.CreateObservableGauge(
            name: "hotel.aggregator.requests.active",
            observeValue: () => Interlocked.Read(ref _activeRequests),
            unit: "requests",
            description: "Number of currently active requests");
    }

    // ===== Counter Methods =====

    public void RecordRoomView(int roomId, string? cacheStatus = null)
    {
        var tags = new TagList
        {
            { "room.id", roomId }
        };

        if (!string.IsNullOrEmpty(cacheStatus))
        {
            tags.Add("cache.status", cacheStatus);
        }

        _roomViewsCounter.Add(1, tags);
    }

    public void RecordCacheHit(string cacheLevel)
    {
        _cacheHitsCounter.Add(1, new TagList
        {
            { "cache.level", cacheLevel }
        });
    }

    public void RecordCacheMiss()
    {
        _cacheMissesCounter.Add(1);
    }

    public void RecordGrpcCall(string serviceName, bool success)
    {
        var tags = new TagList
        {
            { "service.name", serviceName },
            { "status", success ? "success" : "failure" }
        };

        _grpcCallsCounter.Add(1, tags);

        if (!success)
        {
            _grpcErrorsCounter.Add(1, new TagList
            {
                { "service.name", serviceName }
            });
        }
    }

    // ===== Histogram Methods =====

    public void RecordAggregationDuration(double durationMs, int roomId, bool success)
    {
        _aggregationDurationHistogram.Record(durationMs, new TagList
        {
            { "room.id", roomId },
            { "status", success ? "success" : "failure" }
        });
    }

    public void RecordGrpcCallDuration(double durationMs, string serviceName, string operation, bool success)
    {
        _grpcCallDurationHistogram.Record(durationMs, new TagList
        {
            { "service.name", serviceName },
            { "operation", operation },
            { "status", success ? "success" : "failure" }
        });
    }

    public void RecordCacheOperationDuration(double durationMs, string operation, string cacheLevel)
    {
        _cacheOperationDurationHistogram.Record(durationMs, new TagList
        {
            { "operation", operation },
            { "cache.level", cacheLevel }
        });
    }

    // ===== Gauge Methods =====

    public IDisposable TrackActiveRequest()
    {
        Interlocked.Increment(ref _activeRequests);
        return new ActiveRequestTracker(() => Interlocked.Decrement(ref _activeRequests));
    }

    private class ActiveRequestTracker : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public ActiveRequestTracker(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _onDispose();
                _disposed = true;
            }
        }
    }
}
