using HotelReviews.Domain.Entities;
using HotelReviews.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HotelReviews.Infrastructure.Seeding;
public class MongoDataSeeder : IDataSeeder
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoDataSeeder> _logger;

    public MongoDataSeeder(MongoDbContext context, ILogger<MongoDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting MongoDB seeding...");

        await SeedReviewsAsync(cancellationToken);
        await SeedRequestsAsync(cancellationToken);

        _logger.LogInformation("MongoDB seeding completed");
    }
    private async Task SeedReviewsAsync(CancellationToken cancellationToken)
    {
        var collection = _context.Reviews;

        var existingCount = await collection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
        if (existingCount > 0)
        {
            _logger.LogInformation("Requests collection already contains data. Skipping seeding.");
            return;
        }

        var reviews = new List<Review>
        {
            Review.Create(2, 102, 4, "Зручне розташування, але в номері не вистачало чайника."),
            Review.Create(3, 102, 5, "Чудовий номер! Все на найвищому рівні."),
            Review.Create(1, 102, 3, "Непогано, але можна краще. Шумно вночі."),
            Review.Create(1, 101, 5, "Ідеальний номер для сімейного відпочинку! Чисто та затишно."),
            Review.Create(4, 101, 4, "Дуже гарний вид з вікна, персонал привітний."),
            Review.Create(2, 103, 5, "Розкішний номер! Сучасний дизайн та відмінний сервіс."),
            Review.Create(5, 103, 4, "Все сподобалось, окрім ціни. Трохи дорого."),
            Review.Create(3, 104, 2, "Розчарування... Номер не відповідає фото на сайті."),
            Review.Create(6, 104, 3, "Середньо. За ці гроші можна знайти краще."),
            Review.Create(4, 105, 5, "Чудово! Обов'язково повернемось ще раз!"),
        };

        reviews[0].Approve();
        reviews[1].Approve();
        reviews[2].Approve();
        reviews[3].Approve();
        reviews[4].Approve();
        reviews[5].Approve();

        reviews[0].Verify();
        reviews[1].Verify();
        reviews[3].Verify();
        reviews[5].Verify();

        await collection.InsertManyAsync(reviews, cancellationToken: cancellationToken);

        _logger.LogInformation($"Додано {reviews.Count} відгуків до колекції Reviews");
    }
    private async Task SeedRequestsAsync(CancellationToken cancellationToken)
    {
        var collection = _context.Requests;

        var existingCount = await collection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
        if (existingCount > 0)
        {
            _logger.LogInformation("Requests collection already contains data. Skipping seeding.");
            return;
        }

        var requests = new List<Request>
        {
            Request.Create(1, 101, "Чи можна додати дитяче ліжечко в номер?", "accommodation", 3),
            Request.Create(2, 102, "Покращити Wi-Fi у всіх номерах.", "technical", 4),
            Request.Create(3, 103, "Замінити старі рушники та ковдри.", "housekeeping", 5),
            Request.Create(4, 104, "Прошу надати праску та прасувальну дошку.", "housekeeping", 2),
            Request.Create(5, 105, "Чи можна замовити сніданок в номер?", "service", 3),
            Request.Create(1, 102, "Кондиціонер в номері не працює, прошу полагодити.", "maintenance", 5),
            Request.Create(2, 103, "Хотів би пізній виїзд, чи це можливо?", "accommodation", 2),
            Request.Create(6, 101, "Дуже голосно від сусіднього номеру, прошу допомогти.", "complaint", 4),
        };
        requests[1].StartProcessing(1);
        requests[1].AddResponse("Ми працюємо над покращенням Wi-Fi. Очікуйте оновлення протягом тижня.");

        requests[2].Approve(2);
        requests[2].StartProcessing(2);
        requests[2].Complete("Рушники та ковдри замінено. Дякуємо за повідомлення!");

        requests[5].StartProcessing(1);

        await collection.InsertManyAsync(requests, cancellationToken: cancellationToken);

        _logger.LogInformation($"Додано {requests.Count} запитів до колекції Requests");
    }
}