using MassTransit;
using Microsoft.Extensions.Logging;
using HotelPlatform.Aggregator.Services;

namespace HotelPlatform.Aggregator.Consumers;

public class ReservationMadeConsumer : IConsumer<ReservationMadeEvent>
{
    private readonly TwoLevelCacheService _cache;
    private readonly ILogger<ReservationMadeConsumer> _logger;

    public ReservationMadeConsumer(
        TwoLevelCacheService cache,
        ILogger<ReservationMadeConsumer> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task Consume(ConsumeContext<ReservationMadeEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "🔄 Обробка події ReservationMade для кімнати {RoomId}, бронювання {ReservationId}",
            @event.RoomId,
            @event.ReservationId);

        try
        {
            var cacheKey = $"room-details:{@event.RoomId}";
            await _cache.InvalidateAsync(cacheKey);

            _logger.LogInformation(
                "✅ Кеш інвалідовано для кімнати {RoomId} після бронювання {ReservationId}",
                @event.RoomId,
                @event.ReservationId);
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

public record ReservationMadeEvent(
    Guid EventId,
    DateTime OccurredAt,
    string SourceService,
    int ReservationId,
    int ClientId,
    int RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    decimal TotalPrice,
    string Status);
