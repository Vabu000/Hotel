using HotelPlatform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapGet("/", () => Results.Json(new
{
    service = "Hotel Platform API Gateway",
    version = "1.0",
    status = "Running",
    instance = Environment.MachineName,
    description = "YARP Reverse Proxy - всі запити автоматично перенаправляються",
    availableRoutes = new[]
    {
        new { path = "/api/reviews", proxiedTo = "reviews-service" },
        new { path = "/api/hotels", proxiedTo = "catalog-service" },
        new { path = "/api/reservations", proxiedTo = "reservation-service" },
        new { path = "/api/aggregator", proxiedTo = "aggregator" }
    },
    testing = new
    {
        message = "Використовуй прямі HTTP запити",
        examples = new[]
        {
            "GET https://localhost:5001/api/reviews",
            "GET https://localhost:5001/api/hotels",
            "GET https://localhost:5001/health"
        }
    },
    dashboard = "https://localhost:17050"
})).ExcludeFromDescription();

app.UseMiddleware<CorrelationIdMiddleware>();
app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();