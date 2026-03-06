using HotelPlatform.ServiceDefaults.Events;

namespace HotelCatalog.Bll.Events;

/// <summary>
/// Подія яка публікується коли створюється нова категорія кімнат у каталозі
/// Використовується для повідомлення інших сервісів про нову категорію (наприклад для pre-warming кешу)
/// </summary>
public class RoomCategoryCreatedEvent : IIntegrationEvent
{
    /// <summary>
    /// Унікальний ідентифікатор події
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Час коли створено категорію (UTC)
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Назва сервісу джерела ("catalog-service")
    /// </summary>
    public string SourceService { get; init; }

    /// <summary>
    /// ID створеної категорії кімнат
    /// </summary>
    public int RoomCategoryId { get; init; }

    /// <summary>
    /// Назва категорії (наприклад "Deluxe", "Standard")
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Опис категорії (опціонально)
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Вмістимість кімнати (кількість гостей)
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Ціна за ніч
    /// </summary>
    public decimal PricePerNight { get; init; }

    /// <summary>
    /// Створює нову подію про створення категорії кімнат
    /// </summary>
    public RoomCategoryCreatedEvent(
        int roomCategoryId,
        string name,
        string? description,
        int capacity,
        decimal pricePerNight)
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        SourceService = "catalog-service";

        RoomCategoryId = roomCategoryId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        Capacity = capacity;
        PricePerNight = pricePerNight;
    }
}
