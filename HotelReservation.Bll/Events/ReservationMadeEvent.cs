using HotelPlatform.ServiceDefaults.Events;

namespace HotelReservation.Bll.Events;

/// <summary>
/// Подія яка публікується коли створюється нове бронювання
/// Використовується для інформування інших сервісів (наприклад Aggregator) про нове бронювання
/// </summary>
public class ReservationMadeEvent : IIntegrationEvent
{
    /// <summary>
    /// Унікальний ідентифікатор події
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Час коли створено бронювання (UTC)
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Назва сервісу джерела ("reservation-service")
    /// </summary>
    public string SourceService { get; init; }

    /// <summary>
    /// ID створеного бронювання
    /// </summary>
    public int ReservationId { get; init; }

    /// <summary>
    /// ID клієнта який зробив бронювання
    /// </summary>
    public int ClientId { get; init; }

    /// <summary>
    /// ID кімнати яку забронювали
    /// </summary>
    public int RoomId { get; init; }

    /// <summary>
    /// Дата заїзду
    /// </summary>
    public DateTime CheckInDate { get; init; }

    /// <summary>
    /// Дата виїзду
    /// </summary>
    public DateTime CheckOutDate { get; init; }

    /// <summary>
    /// Загальна вартість бронювання
    /// </summary>
    public decimal TotalPrice { get; init; }

    /// <summary>
    /// Статус бронювання (Pending, Confirmed, CheckedIn, CheckedOut, Cancelled)
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Створює нову подію про бронювання
    /// </summary>
    public ReservationMadeEvent(
        int reservationId,
        int clientId,
        int roomId,
        DateTime checkInDate,
        DateTime checkOutDate,
        decimal totalPrice,
        string status)
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        SourceService = "reservation-service";

        ReservationId = reservationId;
        ClientId = clientId;
        RoomId = roomId;
        CheckInDate = checkInDate;
        CheckOutDate = checkOutDate;
        TotalPrice = totalPrice;
        Status = status ?? throw new ArgumentNullException(nameof(status));
    }
}
