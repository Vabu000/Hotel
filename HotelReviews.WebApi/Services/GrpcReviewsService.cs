using Grpc.Core;
using HotelReviews.Application.Common.Interfaces;
using HotelReviews.Application.DTOs;
using HotelReviews.Application.Reviews.Commands.CreateReview;
using HotelReviews.Application.Reviews.Queries.GetReviewById;
using HotelReviews.Application.Reviews.Queries.GetReviewsByRoom;
using HotelReviews.WebApi.Services;
using MediatR;
namespace HotelReviews.WebApi.Services;

public class GrpcReviewsService : ReviewsService.ReviewsServiceBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GrpcReviewsService> _logger;

    public GrpcReviewsService(IMediator mediator, ILogger<GrpcReviewsService> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override Task<GetReviewsByRoomResponse> GetReviewsByRoom(
     GetReviewsByRoomRequest request,
     ServerCallContext context)
    {
        _logger.LogInformation("✅ gRPC GetReviewsByRoom called: RoomId={RoomId}", request.RoomId);

        var response = new GetReviewsByRoomResponse
        {
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0,
            HasPreviousPage = false,
            HasNextPage = false
        };

        return Task.FromResult(response);
    }

    public override async Task<ReviewResponse> GetReviewById(
        GetReviewByIdRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetReviewById: Id={ReviewId}", request.Id);

        var query = new GetReviewByIdQuery(request.Id);
        var result = await _mediator.Send(query, context.CancellationToken);

        if (result == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Review with ID '{request.Id}' not found"));
        }

        return new ReviewResponse
        {
            Review = MapToGrpcMessage(result)
        };
    }

    public override async Task<ReviewResponse> CreateReview(
        CreateReviewRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC CreateReview: RoomId={RoomId}, ClientId={ClientId}",
            request.RoomId, request.ClientId);

        var command = new CreateReviewCommand(
        ClientId: request.ClientId,
        RoomId: request.RoomId,
        Rating: request.Rating,
        Comment: request.Comment
    );

        var result = await _mediator.Send(command, context.CancellationToken);

        return new ReviewResponse
        {
            Review = MapToGrpcMessage(result)
        };
    }

    private static ReviewMessage MapToGrpcMessage(ReviewDto dto)
    {
        return new ReviewMessage
        {
            Id = dto.Id,
            ClientId = dto.ClientId,
            RoomId = dto.RoomId,
            Rating = dto.Rating,
            Comment = dto.Comment ?? string.Empty,
            Date = dto.Date.ToString("O"),
            IsVerified = dto.IsVerified,
            IsApproved = dto.IsApproved,
            RejectionReason = dto.RejectionReason ?? string.Empty,
            CreatedAt = dto.CreatedAt.ToString("O"),
            UpdatedAt = dto.UpdatedAt?.ToString("O") ?? string.Empty
        };
    }
}