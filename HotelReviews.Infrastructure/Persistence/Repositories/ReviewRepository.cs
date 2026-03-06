using HotelReviews.Domain.Entities;
using HotelReviews.Domain.Interfaces;
using MongoDB.Driver;

namespace HotelReviews.Infrastructure.Persistence.Repositories;
public class ReviewRepository : IReviewRepository
{
    private readonly IMongoCollection<Review> _collection;

    public ReviewRepository(MongoDbContext context)
    {
        _collection = context.Reviews;
    }

    public async Task<Review?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(_ => true)
            .ToListAsync(cancellationToken);
    }

    public async Task<Review> CreateAsync(Review review, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(review, cancellationToken: cancellationToken);
        return review;
    }

    public async Task UpdateAsync(Review review, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(
            r => r.Id == review.Id,
            review,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.ClientId == clientId)
            .SortByDescending(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetByRoomIdAsync(int roomId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.RoomId == roomId)
            .SortByDescending(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetByRatingAsync(int rating, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.Rating.Value == rating)
            .SortByDescending(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetVerifiedReviewsAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.IsVerified)
            .SortByDescending(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetApprovedReviewsAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.IsApproved)
            .SortByDescending(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => !r.IsApproved)
            .SortBy(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<double> GetAverageRatingByRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var reviews = await _collection
            .Find(r => r.RoomId == roomId && r.IsApproved)
            .ToListAsync(cancellationToken);

        if (!reviews.Any())
            return 0;

        return reviews.Average(r => r.Rating.Value);
    }

    public async Task<int> GetReviewCountByRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        return (int)await _collection
            .CountDocumentsAsync(r => r.RoomId == roomId, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<int, int>> GetRatingDistributionByRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var reviews = await _collection
            .Find(r => r.RoomId == roomId && r.IsApproved)
            .ToListAsync(cancellationToken);

        return reviews
            .GroupBy(r => r.Rating.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<(IEnumerable<Review> Items, long TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        int? roomId = null,
        int? clientId = null,
        int? minRating = null,
        bool? isVerified = null,
        bool? isApproved = null,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<Review>.Filter;
        var filter = filterBuilder.Empty;

        if (roomId.HasValue)
            filter &= filterBuilder.Eq(r => r.RoomId, roomId.Value);

        if (clientId.HasValue)
            filter &= filterBuilder.Eq(r => r.ClientId, clientId.Value);

        if (minRating.HasValue)
            filter &= filterBuilder.Gte(r => r.Rating.Value, minRating.Value);

        if (isVerified.HasValue)
            filter &= filterBuilder.Eq(r => r.IsVerified, isVerified.Value);

        if (isApproved.HasValue)
            filter &= filterBuilder.Eq(r => r.IsApproved, isApproved.Value);

        var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .SortByDescending(r => r.Date)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IEnumerable<Review>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Review>.Filter.Text(searchTerm);

        return await _collection
            .Find(filter)
            .SortByDescending(r => r.Date)
            .Limit(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        var count = await _collection.CountDocumentsAsync(r => r.Id == id, cancellationToken: cancellationToken);
        return count > 0;
    }

    public async Task<bool> HasClientReviewedRoomAsync(int clientId, int roomId, CancellationToken cancellationToken = default)
    {
        var count = await _collection.CountDocumentsAsync(
            r => r.ClientId == clientId && r.RoomId == roomId,
            cancellationToken: cancellationToken);

        return count > 0;
    }
}