using HotelPlatform.ServiceDefaults.Events;

namespace HotelReviews.Application.Events;

/// <summary>
/// Подія яка публікується коли додається новий відгук про кімнату
/// Використовується для інвалідації кешу відгуків та оновлення агрегованих даних
/// </summary>
public class ReviewAddedEvent : IIntegrationEvent
{
    /// <summary>
    /// Унікальний ідентифікатор події
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Час коли створено відгук (UTC)
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Назва сервісу джерела ("reviews-service")
    /// </summary>
    public string SourceService { get; init; }

    /// <summary>
    /// ID створеного відгуку (MongoDB ObjectId як string)
    /// </summary>
    public string ReviewId { get; init; }

    /// <summary>
    /// ID кімнати для якої залишено відгук
    /// </summary>
    public int RoomId { get; init; }

    /// <summary>
    /// ID клієнта який залишив відгук
    /// </summary>
    public int ClientId { get; init; }

    /// <summary>
    /// Оцінка від 1 до 5 зірок
    /// </summary>
    public int Rating { get; init; }

    /// <summary>
    /// Текстовий коментар (опціонально)
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Створює нову подію про додавання відгуку
    /// </summary>
    public ReviewAddedEvent(
        string reviewId,
        int roomId,
        int clientId,
        int rating,
        string? comment)
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        SourceService = "reviews-service";

        ReviewId = reviewId ?? throw new ArgumentNullException(nameof(reviewId));
        RoomId = roomId;
        ClientId = clientId;
        Rating = rating;
        Comment = comment;
    }
}
