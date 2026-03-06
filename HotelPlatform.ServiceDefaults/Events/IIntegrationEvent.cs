namespace HotelPlatform.ServiceDefaults.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string SourceService { get; }
}
