using Microsoft.AspNetCore.Mvc;
using HotelPlatform.Aggregator.DTOs;
using HotelPlatform.Aggregator.Services;
using HotelCatalog.Api.Grpc;
using HotelReservation.Api.Grpc;
using HotelReviews.WebApi.Services;
using Grpc.Core;
using GrpcStatusCode = Grpc.Core.StatusCode;

namespace HotelPlatform.Aggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomAggregatorController : ControllerBase
{
    private readonly CatalogService.CatalogServiceClient _catalogClient;
    private readonly ReservationsService.ReservationsServiceClient _reservationsClient;
    private readonly ReviewsService.ReviewsServiceClient _reviewsClient;
    private readonly TwoLevelCacheService _cache;
    private readonly MetricsService _metrics;
    private readonly ILogger<RoomAggregatorController> _logger;

    public RoomAggregatorController(
        CatalogService.CatalogServiceClient catalogClient,
        ReservationsService.ReservationsServiceClient reservationsClient,
        ReviewsService.ReviewsServiceClient reviewsClient,
        TwoLevelCacheService cache,
        MetricsService metrics,
        ILogger<RoomAggregatorController> logger)
    {
        _catalogClient = catalogClient;
        _reservationsClient = reservationsClient;
        _reviewsClient = reviewsClient;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
    }

    [HttpGet("room/{roomId}")]
    [ProducesResponseType(typeof(RoomDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoomDetailsDto>> GetRoomDetails(
    int roomId,
    CancellationToken cancellationToken)
    {
        using var _ = _metrics.TrackActiveRequest();

        var cacheKey = $"room-details:{roomId}";
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Aggregating data for room {RoomId}", roomId);

            var result = await _cache.GetOrSetAsync(
                key: cacheKey,
                factory: async () => await AggregateRoomDataAsync(roomId, cancellationToken),
                l1Expiration: TimeSpan.FromMinutes(2),
                l2Expiration: TimeSpan.FromMinutes(10),
                cancellationToken: cancellationToken
            );

            sw.Stop();

            if (result == null)
            {
                _metrics.RecordAggregationDuration(sw.Elapsed.TotalMilliseconds, roomId, success: false);
                return NotFound($"Room with ID {roomId} not found");
            }

            _metrics.RecordRoomView(roomId);
            _metrics.RecordAggregationDuration(sw.Elapsed.TotalMilliseconds, roomId, success: true);

            return Ok(result);
        }
        catch (RpcException ex) when (ex.StatusCode == GrpcStatusCode.NotFound)
        {
            sw.Stop();
            _metrics.RecordAggregationDuration(sw.Elapsed.TotalMilliseconds, roomId, success: false);

            _logger.LogWarning("Room {RoomId} not found in one of the services", roomId);
            return NotFound($"Room with ID {roomId} not found");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordAggregationDuration(sw.Elapsed.TotalMilliseconds, roomId, success: false);

            _logger.LogError(ex, "❌ Error aggregating data for room {RoomId}: {Message}", roomId, ex.Message);
            Console.WriteLine($"❌ AGGREGATOR ERROR: {ex.Message}");
            Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Паралельні gRPC виклики для агрегації даних
    /// </summary>
    private async Task<RoomDetailsDto?> AggregateRoomDataAsync(int roomId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling gRPC services for room {RoomId}", roomId);

        var roomTask = GetRoomInfoAsync(roomId, cancellationToken);
        var categoryTask = GetRoomCategoryAsync(roomId, cancellationToken);
        var reviewsTask = GetReviewsAsync(roomId, cancellationToken);
        var reservationsTask = GetReservationsAsync(roomId, cancellationToken);

        await Task.WhenAll(roomTask, categoryTask, reviewsTask, reservationsTask);

        var room = await roomTask;
        var category = await categoryTask;
        var reviews = await reviewsTask;
        var reservations = await reservationsTask;

        if (room == null)
        {
            return null;
        }

        var averageRating = reviews.Any()
            ? reviews.Average(r => r.Rating)
            : 0;

        var isAvailable = !reservations.Any(r =>
            r.Status == "Confirmed" || r.Status == "CheckedIn");

        return new RoomDetailsDto
        {
            RoomId = room.RoomId,
            RoomNumber = room.RoomNumber,
            Capacity = room.Capacity,
            Price = room.Price,
            Category = category,
            Reviews = reviews,
            AverageRating = Math.Round(averageRating, 2),
            TotalReviews = reviews.Count,
            Reservations = reservations,
            IsAvailable = isAvailable
        };
    }

    /// <summary>
    /// gRPC: отримати інфо про кімнату з Reservation сервісу
    /// </summary>
    private async Task<RoomDetailsDto?> GetRoomInfoAsync(int roomId, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = new GetRoomByIdRequest { RoomId = roomId };
            var response = await _reservationsClient.GetRoomByIdAsync(request, cancellationToken: cancellationToken);

            sw.Stop();
            _metrics.RecordGrpcCall("reservation-service", success: true);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "reservation-service", "GetRoomById", success: true);

            _logger.LogInformation("✅ gRPC: Got room info for {RoomId}", roomId);

            return new RoomDetailsDto
            {
                RoomId = response.Room.RoomId,
                RoomNumber = response.Room.RoomNumber,
                Capacity = response.Room.Capacity,
                Price = (decimal)response.Room.Price
            };
        }
        catch (RpcException ex)
        {
            sw.Stop();
            _metrics.RecordGrpcCall("reservation-service", success: false);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "reservation-service", "GetRoomById", success: false);

            _logger.LogWarning("⚠️ gRPC: Failed to get room {RoomId}: {Status}", roomId, ex.StatusCode);
            throw;
        }
    }

    /// <summary>
    /// gRPC: отримати категорію кімнати з Catalog сервісу
    /// </summary>
    private async Task<RoomCategoryInfo?> GetRoomCategoryAsync(int roomId, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = new GetRoomCategoryByRoomIdRequest { RoomId = roomId };
            var response = await _catalogClient.GetRoomCategoryByRoomIdAsync(request, cancellationToken: cancellationToken);

            sw.Stop();
            _metrics.RecordGrpcCall("catalog-service", success: true);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "catalog-service", "GetRoomCategoryByRoomId", success: true);

            _logger.LogInformation("✅ gRPC: Got category for room {RoomId}", roomId);

            return new RoomCategoryInfo
            {
                CategoryId = response.Category.CategoryId,
                Name = response.Category.Name,
                Description = response.Category.Description,
                Services = response.Category.Services.Select(s => new ServiceInfo
                {
                    ServiceId = s.ServiceId,
                    Name = s.Name,
                    Description = s.Description,
                    Price = (decimal)s.Price
                }).ToList()
            };
        }
        catch (RpcException ex)
        {
            sw.Stop();
            _metrics.RecordGrpcCall("catalog-service", success: false);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "catalog-service", "GetRoomCategoryByRoomId", success: false);

            _logger.LogWarning("⚠️ gRPC: Failed to get category for room {RoomId}: {Status}", roomId, ex.StatusCode);
            return null;
        }
    }

    /// <summary>
    /// gRPC: отримати відгуки з Reviews сервісу
    /// </summary>
    private async Task<List<ReviewInfo>> GetReviewsAsync(int roomId, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = new GetReviewsByRoomRequest
            {
                RoomId = roomId,
                Page = 1,
                PageSize = 100,
                IsApproved = true
            };

            var response = await _reviewsClient.GetReviewsByRoomAsync(request, cancellationToken: cancellationToken);

            sw.Stop();
            _metrics.RecordGrpcCall("reviews-service", success: true);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "reviews-service", "GetReviewsByRoom", success: true);

            _logger.LogInformation("✅ gRPC: Got {Count} reviews for room {RoomId}",
                response.Items.Count, roomId);

            return response.Items.Select(r => new ReviewInfo
            {
                Id = r.Id,
                ClientId = r.ClientId,
                Rating = r.Rating,
                Comment = r.Comment,
                Date = DateTime.Parse(r.Date),
                IsVerified = r.IsVerified,
                IsApproved = r.IsApproved
            }).ToList();
        }
        catch (RpcException ex)
        {
            sw.Stop();
            _metrics.RecordGrpcCall("reviews-service", success: false);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "reviews-service", "GetReviewsByRoom", success: false);

            _logger.LogWarning("⚠️ gRPC: Failed to get reviews for room {RoomId}: {Status}", roomId, ex.StatusCode);
            return new List<ReviewInfo>();
        }
    }

    /// <summary>
    /// gRPC: отримати бронювання з Reservation сервісу
    /// </summary>
    private async Task<List<ReservationInfo>> GetReservationsAsync(int roomId, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var request = new GetReservationsByRoomRequest
            {
                RoomId = roomId,
                Page = 1,
                PageSize = 100
            };

            var response = await _reservationsClient.GetReservationsByRoomAsync(request, cancellationToken: cancellationToken);

            sw.Stop();
            _metrics.RecordGrpcCall("reservation-service", success: true);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "reservation-service", "GetReservationsByRoom", success: true);

            _logger.LogInformation("✅ gRPC: Got {Count} reservations for room {RoomId}",
                response.Items.Count, roomId);

            return response.Items.Select(r => new ReservationInfo
            {
                ReservationId = r.Id,
                ClientId = r.ClientId,
                CheckInDate = DateTime.Parse(r.CheckInDate),
                CheckOutDate = DateTime.Parse(r.CheckOutDate),
                Status = r.Status,
                TotalPrice = (decimal)r.TotalPrice
            }).ToList();
        }
        catch (RpcException ex)
        {
            sw.Stop();
            _metrics.RecordGrpcCall("reservation-service", success: false);
            _metrics.RecordGrpcCallDuration(sw.Elapsed.TotalMilliseconds, "reservation-service", "GetReservationsByRoom", success: false);

            _logger.LogWarning("⚠️ gRPC: Failed to get reservations for room {RoomId}: {Status}", roomId, ex.StatusCode);
            return new List<ReservationInfo>();
        }
    }

    /// <summary>
    /// Інвалідація кешу для кімнати (викликати після зміни даних)
    /// </summary>
    [HttpDelete("room/{roomId}/cache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InvalidateRoomCache(int roomId)
    {
        var cacheKey = $"room-details:{roomId}";
        await _cache.InvalidateAsync(cacheKey);

        _logger.LogInformation("Cache invalidated for room {RoomId}", roomId);
        return NoContent();
    }
}