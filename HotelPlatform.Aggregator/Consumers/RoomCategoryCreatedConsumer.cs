using MassTransit;
using Microsoft.Extensions.Logging;

namespace HotelPlatform.Aggregator.Consumers;

public class RoomCategoryCreatedConsumer : IConsumer<RoomCategoryCreatedEvent>
{
    private readonly ILogger<RoomCategoryCreatedConsumer> _logger;

    public RoomCategoryCreatedConsumer(ILogger<RoomCategoryCreatedConsumer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<RoomCategoryCreatedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "🔄 Обробка події RoomCategoryCreated для категорії {CategoryId}: {CategoryName}",
            @event.RoomCategoryId,
            @event.Name);

        await Task.CompletedTask;

        _logger.LogInformation(
            "✅ Подію RoomCategoryCreated оброблено для категорії {CategoryId}",
            @event.RoomCategoryId);
    }
}

public record RoomCategoryCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    string SourceService,
    int RoomCategoryId,
    string Name,
    string Description,
    int Capacity,
    decimal PricePerNight);
