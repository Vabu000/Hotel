using MassTransit;
using Microsoft.Extensions.Logging;

namespace HotelPlatform.ServiceDefaults.Events;

/// <summary>
/// Реалізація публікатора подій через MassTransit та RabbitMQ
/// </summary>
public class RabbitMQEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RabbitMQEventPublisher> _logger;

    public RabbitMQEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<RabbitMQEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var eventType = @event.GetType().Name;

        try
        {
            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation(
                "Успішно опубліковано подію {EventType} з ID {EventId} з сервісу {SourceService}",
                eventType,
                @event.EventId,
                @event.SourceService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Помилка при публікації події {EventType} з ID {EventId}",
                eventType,
                @event.EventId);
            throw;
        }
    }
}
