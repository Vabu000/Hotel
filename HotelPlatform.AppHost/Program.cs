Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
var builder = DistributedApplication.CreateBuilder(args);

// MongoDB не використовується - Reviews Service підключається до standalone MongoDB
// var mongodb = builder.AddMongoDB("mongodb")
//     .WithDataVolume("mongodb-data")
//     .WithMongoExpress();

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("postgres-data")
    .WithPgAdmin();

var redis = builder.AddRedis("redis")
    .WithDataVolume("redis-data")
    .WithRedisCommander();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume("rabbitmq-data")
    .WithManagementPlugin();

// var mongoDatabase = mongodb.AddDatabase("hotel-reviews-dev");
var reservationDb = postgres.AddDatabase("HotelReservationDb");
var catalogDb = postgres.AddDatabase("HotelCatalogDb");

var reviewsService = builder.AddProject<Projects.HotelReviews_WebApi>("reviews-service")
    .WithReference(redis)
    .WithReference(rabbitmq);

var reservationService = builder.AddProject<Projects.HotelReservation_Api>("reservation-service")
    .WithReference(reservationDb)
    .WithReference(redis)
    .WithReference(rabbitmq);

var catalogService = builder.AddProject<Projects.HotelCatalog_Api>("catalog-service")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WithReference(rabbitmq);

var aggregator = builder.AddProject<Projects.HotelPlatform_Aggregator>("aggregator")
    .WithReference(reviewsService)
    .WithReference(reservationService)
    .WithReference(catalogService)
    .WithReference(redis);

var gateway = builder.AddProject<Projects.HotelPlatform_ApiGateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(reviewsService)
    .WithReference(reservationService)
    .WithReference(catalogService)
    .WithReference(aggregator);

builder.Build().Run();