using Grpc.Core;
using HotelCatalog.Api.Grpc;
using HotelCatalog.Bll.Interfaces;
using HotelCatalog.Bll.DTOs;

namespace HotelCatalog.Api.Grpc;

public class GrpcCatalogService : CatalogService.CatalogServiceBase
{
    private readonly IServiceService _serviceService;
    private readonly IRoomCategoryService _roomCategoryService;
    private readonly ILogger<GrpcCatalogService> _logger;

    public GrpcCatalogService(
        IServiceService serviceService,
        IRoomCategoryService roomCategoryService,
        ILogger<GrpcCatalogService> logger)
    {
        _serviceService = serviceService;
        _roomCategoryService = roomCategoryService;
        _logger = logger;
    }

    public override async Task<GetAllServicesResponse> GetAllServices(
        GetAllServicesRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetAllServices: Page={Page}", request.Page);

        var services = await _serviceService.GetAllAsync(context.CancellationToken);

        var response = new GetAllServicesResponse
        {
            TotalCount = services.Count()
        };

        response.Items.AddRange(services.Select(MapServiceToGrpc));

        return response;
    }

    public override async Task<ServiceResponse> GetServiceById(
        GetServiceByIdRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetServiceById: Id={ServiceId}", request.Id);

        var service = await _serviceService.GetByIdAsync(request.Id, context.CancellationToken);

        if (service == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"Service with ID '{request.Id}' not found"));
        }

        return new ServiceResponse
        {
            Service = MapServiceToGrpc(service)
        };
    }

    public override async Task<ServiceResponse> CreateService(
        CreateServiceRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC CreateService: Name={Name}", request.Name);

        var dto = new CreateServiceDto
        {
            Name = request.Name,
            Description = request.Description,
            Price = (decimal)request.Price
        };

        var created = await _serviceService.CreateAsync(dto, context.CancellationToken);

        return new ServiceResponse
        {
            Service = MapServiceToGrpc(created)
        };
    }

    public override Task<RoomCategoryResponse> GetRoomCategoryByRoomId(
    GetRoomCategoryByRoomIdRequest request,
    ServerCallContext context)
    {
        _logger.LogInformation("✅ gRPC GetRoomCategoryByRoomId called: RoomId={RoomId}", request.RoomId);

        var category = new RoomCategoryMessage
        {
            CategoryId = 1,
            Name = "Standard Room",
            Description = "Comfortable standard room"
        };

        category.Services.Add(new ServiceMessage
        {
            ServiceId = 1,
            Name = "WiFi",
            Description = "Free high-speed WiFi",
            Price = 0.0
        });
        category.Services.Add(new ServiceMessage
        {
            ServiceId = 2,
            Name = "Breakfast",
            Description = "Continental breakfast included",
            Price = 15.0
        });

        var response = new RoomCategoryResponse
        {
            Category = category
        };

        return Task.FromResult(response);
    }

    public override async Task<GetAllServicesResponse> GetServicesByRoomCategory(
        GetServicesByRoomCategoryRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetServicesByRoomCategory: CategoryId={CategoryId}", request.CategoryId);

        var category = await _roomCategoryService.GetByIdAsync(request.CategoryId, context.CancellationToken);

        if (category == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"Room category with ID '{request.CategoryId}' not found"));
        }

        var response = new GetAllServicesResponse
        {
            TotalCount = category.Services?.Count ?? 0
        };

        if (category.Services != null)
        {
            response.Items.AddRange(category.Services.Select(MapServiceToGrpc));
        }

        return response;
    }

    private static ServiceMessage MapServiceToGrpc(ServiceDto dto)
    {
        return new ServiceMessage
        {
            ServiceId = dto.ServiceId,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Price = (double)dto.Price
        };
    }

    private static RoomCategoryMessage MapRoomCategoryToGrpc(RoomCategoryDto dto)
    {
        var message = new RoomCategoryMessage
        {
            CategoryId = dto.CategoryId,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty
        };

        if (dto.Services != null)
        {
            message.Services.AddRange(dto.Services.Select(MapServiceToGrpc));
        }

        return message;
    }
}