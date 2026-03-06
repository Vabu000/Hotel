using Grpc.Core;
using HotelReservation.Api.Grpc;
using HotelReservation.Bll.Services;
using HotelReservation.Bll.DTOs;
using HotelReservation.Domain.Entities;

namespace HotelReservation.Api.Grpc;

public class GrpcReservationsService : ReservationsService.ReservationsServiceBase
{
    private readonly IReservationService _reservationService;
    private readonly IRoomService _roomService;
    private readonly ILogger<GrpcReservationsService> _logger;

    public GrpcReservationsService(
        IReservationService reservationService,
        IRoomService roomService,
        ILogger<GrpcReservationsService> logger)
    {
        _reservationService = reservationService;
        _roomService = roomService;
        _logger = logger;
    }

    public override async Task<GetAllReservationsResponse> GetAllReservations(
        GetAllReservationsRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetAllReservations: Page={Page}", request.Page);

        var reservations = await _reservationService.GetAllAsync(context.CancellationToken);

        var response = new GetAllReservationsResponse
        {
            TotalCount = reservations.Count()
        };

        response.Items.AddRange(reservations.Select(MapToGrpc));

        return response;
    }

    public override async Task<ReservationResponse> GetReservationById(
        GetReservationByIdRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetReservationById: Id={ReservationId}", request.Id);

        var reservation = await _reservationService.GetByIdAsync(request.Id, context.CancellationToken);

        if (reservation == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"Reservation with ID '{request.Id}' not found"));
        }

        return new ReservationResponse
        {
            Reservation = MapToGrpc(reservation)
        };
    }

    public override async Task<GetAllReservationsResponse> GetReservationsByRoom(
        GetReservationsByRoomRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetReservationsByRoom: RoomId={RoomId}, Page={Page}",
            request.RoomId, request.Page);

        var allReservations = await _reservationService.GetAllAsync(context.CancellationToken);

        var roomReservations = allReservations
            .Where(r => r.RoomId == request.RoomId)
            .ToList();

        var response = new GetAllReservationsResponse
        {
            TotalCount = roomReservations.Count
        };

        response.Items.AddRange(roomReservations.Select(MapToGrpc));

        return response;
    }

    public override async Task<GetAllReservationsResponse> GetReservationsByClient(
        GetReservationsByClientRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetReservationsByClient: ClientId={ClientId}, Page={Page}",
            request.ClientId, request.Page);

        var allReservations = await _reservationService.GetAllAsync(context.CancellationToken);

        var clientReservations = allReservations
            .Where(r => r.ClientId == request.ClientId)
            .ToList();

        var response = new GetAllReservationsResponse
        {
            TotalCount = clientReservations.Count
        };

        response.Items.AddRange(clientReservations.Select(MapToGrpc));

        return response;
    }

    public override Task<RoomResponse> GetRoomById(
     GetRoomByIdRequest request,
     ServerCallContext context)
    {
        _logger.LogInformation("✅ gRPC GetRoomById called: RoomId={RoomId}", request.RoomId);

        var response = new RoomResponse
        {
            Room = new RoomMessage
            {
                RoomId = request.RoomId,
                RoomNumber = $"Room-{request.RoomId}",
                Capacity = 2,
                Price = 150.0,
                CategoryId = 1
            }
        };

        return Task.FromResult(response);
    }

    public override async Task<GetAllReservationsResponse> GetReservationsByHotel(
        GetReservationsByHotelRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetReservationsByHotel: HotelId={HotelId}", request.HotelId);

        var reservations = await _reservationService.GetAllAsync(context.CancellationToken);

        var response = new GetAllReservationsResponse
        {
            TotalCount = reservations.Count()
        };

        response.Items.AddRange(reservations.Select(MapToGrpc));

        return response;
    }

    public override async Task<ReservationResponse> CreateReservation(
        CreateReservationRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC CreateReservation: ClientId={ClientId}, RoomId={RoomId}",
            request.ClientId, request.RoomId);

        var dto = new CreateReservationDto
        {
            ClientId = request.ClientId,
            RoomId = request.RoomId,
            CheckInDate = DateTime.Parse(request.CheckInDate),
            CheckOutDate = DateTime.Parse(request.CheckOutDate)
        };

        var created = await _reservationService.CreateAsync(dto, context.CancellationToken);

        return new ReservationResponse
        {
            Reservation = MapToGrpc(created)
        };
    }

    private static ReservationMessage MapToGrpc(ReservationDto dto)
    {
        double totalPrice = 0.0;
        if (dto.Payments != null && dto.Payments.Any())
        {
            try
            {
                totalPrice = dto.Payments.Sum(p => (double)p.Amount);
            }
            catch
            {
                totalPrice = 0.0;
            }
        }

        return new ReservationMessage
        {
            Id = dto.ReservationId,
            HotelId = 0,
            ClientId = dto.ClientId,
            RoomId = dto.RoomId,
            CheckInDate = dto.CheckInDate.ToString("O"),
            CheckOutDate = dto.CheckOutDate.ToString("O"),
            TotalPrice = totalPrice,
            Status = dto.Status.ToString(),
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
    }

    private static RoomMessage MapRoomToGrpc(RoomDto dto)
    {
        return new RoomMessage
        {
            RoomId = dto.RoomId,
            RoomNumber = dto.RoomNumber ?? string.Empty,
            Capacity = dto.Capacity,
            Price = (double)dto.Price,
            CategoryId = 0 
        };
    }
}