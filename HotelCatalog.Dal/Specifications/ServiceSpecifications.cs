using Ardalis.Specification;
using HotelCatalog.Domain.Entities;

namespace HotelCatalog.Dal.Specifications
{
    /// <summary>
    /// Специфікація для фільтрації сервісів за діапазоном цін
    /// </summary>
    public class ServicesByPriceRangeSpec : Specification<Service>
    {
        public ServicesByPriceRangeSpec(decimal minPrice, decimal maxPrice)
        {
            Query
                .Where(s => s.Price >= minPrice && s.Price <= maxPrice)
                .OrderBy(s => s.Price);
        }
    }

    /// <summary>
    /// Специфікація для завантаження сервісів разом з їх категоріями кімнат (Include)
    /// </summary>
    public class ServicesWithCategoriesSpec : Specification<Service>
    {
        public ServicesWithCategoriesSpec()
        {
            Query
                .Include(s => s.RoomServices)
                    .ThenInclude(rs => rs.RoomCategory);
        }
    }

    /// <summary>
    /// Специфікація для отримання одного сервісу за ID разом з категоріями
    /// </summary>
    public class ServiceByIdWithCategoriesSpec : Specification<Service>, ISingleResultSpecification<Service>
    {
        public ServiceByIdWithCategoriesSpec(int serviceId)
        {
            Query
                .Where(s => s.ServiceId == serviceId)
                .Include(s => s.RoomServices)
                    .ThenInclude(rs => rs.RoomCategory);
        }
    }

    /// <summary>
    /// Специфікація для пагінації сервісів з фільтрацією та сортуванням
    /// Підтримує пошук за назвою/описом, фільтрацію за ціною та сортування
    /// </summary>
    public class ServicesPaginatedSpec : Specification<Service>
    {
        /// <summary>
        /// Створює специфікацію для пагінованого списку сервісів
        /// </summary>
        /// <param name="skip">Скільки записів пропустити (для pagination)</param>
        /// <param name="take">Скільки записів взяти (розмір сторінки)</param>
        /// <param name="searchTerm">Пошуковий термін для фільтрації за назвою або описом</param>
        /// <param name="minPrice">Мінімальна ціна (включно)</param>
        /// <param name="maxPrice">Максимальна ціна (включно)</param>
        /// <param name="sortBy">Поле для сортування ("name", "price")</param>
        /// <param name="sortDescending">Чи сортувати у зворотному порядку</param>
        public ServicesPaginatedSpec(
            int skip,
            int take,
            string? searchTerm = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            bool sortDescending = false)
        {
            // Filtering
            if (!string.IsNullOrEmpty(searchTerm))
            {
                Query.Where(s =>
                    s.Name.Contains(searchTerm) ||
                    (s.Description != null && s.Description.Contains(searchTerm)));
            }

            if (minPrice.HasValue)
            {
                Query.Where(s => s.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                Query.Where(s => s.Price <= maxPrice.Value);
            }

            // Sorting
            if (!string.IsNullOrEmpty(sortBy))
            {
                switch (sortBy.ToLower())
                {
                    case "name":
                        if (sortDescending)
                            Query.OrderByDescending(s => s.Name);
                        else
                            Query.OrderBy(s => s.Name);
                        break;
                    case "price":
                        if (sortDescending)
                            Query.OrderByDescending(s => s.Price);
                        else
                            Query.OrderBy(s => s.Price);
                        break;
                    default:
                        Query.OrderBy(s => s.ServiceId);
                        break;
                }
            }
            else
            {
                Query.OrderBy(s => s.ServiceId);
            }

            // Pagination
            Query
                .Skip(skip)
                .Take(take);
        }
    }

    /// <summary>
    /// Специфікація для підрахунку кількості сервісів з фільтрацією
    /// Використовується разом з ServicesPaginatedSpec для отримання TotalCount
    /// </summary>
    public class ServicesCountSpec : Specification<Service>
    {
        /// <summary>
        /// Створює специфікацію для підрахунку сервісів з фільтрацією
        /// </summary>
        /// <param name="searchTerm">Пошуковий термін</param>
        /// <param name="minPrice">Мінімальна ціна</param>
        /// <param name="maxPrice">Максимальна ціна</param>
        public ServicesCountSpec(
            string? searchTerm = null,
            decimal? minPrice = null,
            decimal? maxPrice = null)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                Query.Where(s =>
                    s.Name.Contains(searchTerm) ||
                    (s.Description != null && s.Description.Contains(searchTerm)));
            }

            if (minPrice.HasValue)
            {
                Query.Where(s => s.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                Query.Where(s => s.Price <= maxPrice.Value);
            }
        }
    }
}