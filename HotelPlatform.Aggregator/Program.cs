using HotelPlatform.Aggregator.Services;
using HotelPlatform.Aggregator.HealthChecks;
using HotelPlatform.Aggregator.Consumers;
using HotelCatalog.Api.Grpc;
using HotelReservation.Api.Grpc;
using HotelReviews.WebApi.Services;
using HotelPlatform.ServiceDefaults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using MassTransit;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024;
    options.CompactionPercentage = 0.25;
});

builder.AddRedisClient("redis");

builder.Services.AddSingleton<MemoryCacheService>();
builder.Services.AddSingleton<RedisCacheService>();
builder.Services.AddSingleton<TwoLevelCacheService>();

builder.Services.AddSingleton<MetricsService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ReservationMadeConsumer>();
    x.AddConsumer<RoomCategoryCreatedConsumer>();
    x.AddConsumer<ReviewAddedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("rabbitmq");

        if (!string.IsNullOrEmpty(connectionString))
        {
            cfg.Host(connectionString);
        }
        else
        {
            cfg.Host("localhost", "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });
        }

        cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

        cfg.ConfigureEndpoints(context);
    });
});

var catalogServiceUrl = builder.Configuration["GrpcServices:CatalogService"]
    ?? builder.Configuration["services__catalog-service__http__0"]
    ?? builder.Configuration["services:catalog-service:http:0"]
    ?? "http://localhost:5045"; 

Console.WriteLine($"[Aggregator] Catalog Service URL: {catalogServiceUrl}");

builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(options =>
{
    options.Address = new Uri(catalogServiceUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
    EnableMultipleHttp2Connections = true
});

var reservationServiceUrl = builder.Configuration["GrpcServices:ReservationService"]
    ?? builder.Configuration["services__reservation-service__http__0"]
    ?? builder.Configuration["services:reservation-service:http:0"]
    ?? "http://localhost:5151";  

Console.WriteLine($"[Aggregator] Reservation Service URL: {reservationServiceUrl}");

builder.Services.AddGrpcClient<ReservationsService.ReservationsServiceClient>(options =>
{
    options.Address = new Uri(reservationServiceUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
    EnableMultipleHttp2Connections = true
});

var reviewsServiceUrl = builder.Configuration["GrpcServices:ReviewsService"]
    ?? builder.Configuration["services__reviews-service__http__0"]
    ?? builder.Configuration["services:reviews-service:http:0"]
    ?? "http://localhost:5050";  

Console.WriteLine($"[Aggregator] Reviews Service URL: {reviewsServiceUrl}");

builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(options =>
{
    options.Address = new Uri(reviewsServiceUrl);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
    EnableMultipleHttp2Connections = true
});

// ✅ Health Checks
builder.Services.AddHealthChecks()
   
    .AddCheck<RedisHealthCheck>(
        name: "redis_l2_cache",
        tags: new[] { "cache", "redis", "readiness" })
    .AddCheck<CatalogServiceHealthCheck>(
        name: "catalog_service_grpc",
        tags: new[] { "grpc", "downstream", "readiness" })
    .AddCheck<ReservationServiceHealthCheck>(
        name: "reservation_service_grpc",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "grpc", "downstream", "readiness" })
    .AddCheck<ReviewsServiceHealthCheck>(
        name: "reviews_service_grpc",
        tags: new[] { "grpc", "downstream", "readiness" });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Hotel Aggregator API",
        Version = "v1",
        Description = "Aggregates data from Catalog, Reservation and Reviews microservices using gRPC"
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Aggregator API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    message = "Aggregator service is alive"
}))
.WithName("Liveness")
.WithTags("Health")
.ExcludeFromDescription();

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("readiness"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration,
                tags = e.Value.Tags,
                data = e.Value.Data
            })
        });
        await context.Response.WriteAsync(result);
    }
})
.WithName("Readiness")
.WithTags("Health");

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration,
                exception = e.Value.Exception?.Message,
                tags = e.Value.Tags,
                data = e.Value.Data
            })
        }, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        await context.Response.WriteAsync(result);
    }
})
.WithName("Health")
.WithTags("Health");

app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

app.Run();