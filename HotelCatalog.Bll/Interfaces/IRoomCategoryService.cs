using HotelCatalog.Bll.DTOs;

namespace HotelCatalog.Bll.Interfaces
{
    public interface IRoomCategoryService
    {
        Task<RoomCategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<RoomCategoryDto?> GetByIdWithServicesAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<RoomCategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<RoomCategoryDto>> GetCategoriesWithServiceCountAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ServiceDto>> GetServicesByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);
        Task<RoomCategoryDto> CreateAsync(CreateRoomCategoryDto dto, CancellationToken cancellationToken = default);
        Task<RoomCategoryDto> UpdateAsync(UpdateRoomCategoryDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> AssignServiceToCategoryAsync(AssignServiceToCategoryDto dto, CancellationToken cancellationToken = default);
        Task<bool> RemoveServiceFromCategoryAsync(int categoryId, int serviceId, CancellationToken cancellationToken = default);
        Task<PagedResult<RoomCategoryDto>> GetPagedWithSpecificationAsync(RoomCategoryQueryParameters parameters, CancellationToken cancellationToken = default);
    }
}