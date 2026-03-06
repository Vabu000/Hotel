using AutoMapper;
using HotelReviews.Application.Common.Interfaces;
using HotelReviews.Application.DTOs;
using HotelReviews.Application.Events;
using HotelReviews.Domain.Entities;
using HotelReviews.Domain.Interfaces;
using HotelPlatform.ServiceDefaults.Events;
using MediatR;

namespace HotelReviews.Application.Reviews.Commands.CreateReview;
public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IMapper _mapper;
    private readonly ICacheService _cacheService;
    private readonly IEventPublisher _eventPublisher;

    public CreateReviewCommandHandler(
        IReviewRepository reviewRepository,
        IMapper mapper,
        ICacheService cacheService,
        IEventPublisher eventPublisher)
    {
        _reviewRepository = reviewRepository;
        _mapper = mapper;
        _cacheService = cacheService;
        _eventPublisher = eventPublisher;
    }

    public async Task<ReviewDto> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var review = Review.Create(
            request.ClientId,
            request.RoomId,
            request.Rating,
            request.Comment);

        var createdReview = await _reviewRepository.CreateAsync(review, cancellationToken);
        await _cacheService.RemoveByPrefixAsync($"reviews:room:{request.RoomId}", cancellationToken);

        // ✅ Publish ReviewAddedEvent to RabbitMQ
        var reviewEvent = new ReviewAddedEvent(
            reviewId: createdReview.Id,
            roomId: createdReview.RoomId,
            clientId: createdReview.ClientId,
            rating: createdReview.Rating,
            comment: createdReview.Comment);

        await _eventPublisher.PublishAsync(reviewEvent, cancellationToken);

        return _mapper.Map<ReviewDto>(createdReview);
    }
}