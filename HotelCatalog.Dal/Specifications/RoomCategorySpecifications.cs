using Ardalis.Specification;
using HotelCatalog.Domain.Entities;

namespace HotelCatalog.Dal.Specifications
{
    /// <summary>
    /// Специфікація для отримання категорій кімнат відсортованих за кількістю сервісів
    /// </summary>
    public class CategoriesWithServiceCountSpec : Specification<RoomCategory>
    {
        public CategoriesWithServiceCountSpec()
        {
            Query
                .Include(rc => rc.RoomServices)
                .OrderByDescending(rc => rc.RoomServices.Count);
        }
    }

    /// <summary>
    /// Специфікація для отримання однієї категорії за ID разом з її сервісами
    /// </summary>
    public class CategoryByIdWithServicesSpec : Specification<RoomCategory>, ISingleResultSpecification<RoomCategory>
    {
        public CategoryByIdWithServicesSpec(int categoryId)
        {
            Query
                .Where(rc => rc.CategoryId == categoryId)
                .Include(rc => rc.RoomServices)
                    .ThenInclude(rs => rs.Service);
        }
    }

    /// <summary>
    /// Специфікація для пошуку категорій за назвою або описом
    /// </summary>
    public class CategoriesByNameSpec : Specification<RoomCategory>
    {
        public CategoriesByNameSpec(string searchTerm)
        {
            Query
                .Where(rc => rc.Name.Contains(searchTerm) ||
                            (rc.Description != null && rc.Description.Contains(searchTerm)))
                .OrderBy(rc => rc.Name);
        }
    }

    /// <summary>
    /// Специфікація для пагінації категорій кімнат з фільтрацією та сортуванням
    /// </summary>
    public class RoomCategoriesPaginatedSpec : Specification<RoomCategory>
    {
        /// <summary>
        /// Створює специфікацію для пагінованого списку категорій
        /// </summary>
        /// <param name="skip">Скільки записів пропустити</param>
        /// <param name="take">Скільки записів взяти</param>
        /// <param name="searchTerm">Пошуковий термін для фільтрації</param>
        /// <param name="sortBy">Поле для сортування ("name", "description")</param>
        /// <param name="sortDescending">Чи сортувати у зворотному порядку</param>
        public RoomCategoriesPaginatedSpec(
            int skip,
            int take,
            string? searchTerm = null,
            string? sortBy = null,
            bool sortDescending = false)
        {
            // Filtering
            if (!string.IsNullOrEmpty(searchTerm))
            {
                Query.Where(rc =>
                    rc.Name.Contains(searchTerm) ||
                    (rc.Description != null && rc.Description.Contains(searchTerm)));
            }

            // Sorting
            if (!string.IsNullOrEmpty(sortBy))
            {
                switch (sortBy.ToLower())
                {
                    case "name":
                        if (sortDescending)
                            Query.OrderByDescending(rc => rc.Name);
                        else
                            Query.OrderBy(rc => rc.Name);
                        break;
                    case "description":
                        if (sortDescending)
                            Query.OrderByDescending(rc => rc.Description);
                        else
                            Query.OrderBy(rc => rc.Description);
                        break;
                    default:
                        Query.OrderBy(rc => rc.CategoryId);
                        break;
                }
            }
            else
            {
                Query.OrderBy(rc => rc.CategoryId);
            }

            // Pagination
            Query
                .Skip(skip)
                .Take(take);
        }
    }

    /// <summary>
    /// Специфікація для підрахунку кількості категорій кімнат з фільтрацією
    /// </summary>
    public class RoomCategoriesCountSpec : Specification<RoomCategory>
    {
        /// <summary>
        /// Створює специфікацію для підрахунку категорій
        /// </summary>
        /// <param name="searchTerm">Пошуковий термін для фільтрації</param>
        public RoomCategoriesCountSpec(string? searchTerm = null)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                Query.Where(rc =>
                    rc.Name.Contains(searchTerm) ||
                    (rc.Description != null && rc.Description.Contains(searchTerm)));
            }
        }
    }
}