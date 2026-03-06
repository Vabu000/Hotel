namespace HotelPlatform.ServiceDefaults.Events;

/// <summary>
/// Інтерфейс для публікації інтеграційних подій у брокер повідомлень (RabbitMQ)
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
