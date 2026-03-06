using MassTransit;
using Microsoft.Extensions.Logging;
using HotelPlatform.Aggregator.Services;

namespace HotelPlatform.Aggregator.Consumers;

public class ReviewAddedConsumer : IConsumer<ReviewAddedEvent>
{
    private readonly TwoLevelCacheService _cache;
    private readonly ILogger<ReviewAddedConsumer> _logger;

    public ReviewAddedConsumer(
        TwoLevelCacheService cache,
        ILogger<ReviewAddedConsumer> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<ReviewAddedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "🔄 Обробка події ReviewAdded для кімнати {RoomId}, відгук {ReviewId}, оцінка: {Rating}",
            @event.RoomId,
            @event.ReviewId,
            @event.Rating);

        try
        {
            var cacheKey = $"room-details:{@event.RoomId}";
            await _cache.InvalidateAsync(cacheKey);

            _logger.LogInformation(
                "✅ Кеш інвалідовано для кімнати {RoomId} після відгуку {ReviewId}",
                @event.RoomId,
                @event.ReviewId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Помилка при інвалідації кешу для кімнати {RoomId}",
                @event.RoomId);
            throw; 
        }
    }
}

public record ReviewAddedEvent(
    Guid EventId,
    DateTime OccurredAt,
    string SourceService,
    string ReviewId,
    int RoomId,
    int ClientId,
    int Rating,
    string Comment);
