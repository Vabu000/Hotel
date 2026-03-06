using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using HotelCatalog.Dal.Data;
using HotelCatalog.Dal.Interfaces;
using HotelCatalog.Dal.Repositories;
using HotelCatalog.Bll.Interfaces;
using HotelCatalog.Bll.Services;
using HotelCatalog.Bll.Profiles;
using HotelCatalog.Api.Middleware;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.AddDbContext<HotelCatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HotelCatalogDB")));

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<MappingProfile>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IRoomCategoryRepository, RoomCategoryRepository>();
builder.Services.AddScoped<IDiscountCategoryRepository, DiscountCategoryRepository>();

builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IRoomCategoryService, RoomCategoryService>();
builder.Services.AddScoped<IDiscountCategoryService, DiscountCategoryService>();

// Add Event Publisher with MassTransit
builder.Services.AddEventPublisher(builder);

builder.Services.AddGrpc();
builder.Services.AddGrpcHealthChecks();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Hotel Catalog API",
        Version = "v1",
        Description = "API for hotel catalog management with EF Core"
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HotelCatalogDbContext>();
    var maxRetries = 10;
    var delay = TimeSpan.FromSeconds(2);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Log.Information("Attempting database migration (attempt {Attempt}/{MaxRetries})...", i + 1, maxRetries);
            dbContext.Database.Migrate();
            Log.Information("Database migration completed successfully");
            break;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "57P03" && i < maxRetries - 1)
        {
            Log.Warning("Database is still starting up. Waiting {Delay} seconds before retry {Attempt}/{MaxRetries}...",
                delay.TotalSeconds, i + 1, maxRetries);
            Thread.Sleep(delay);
        }
        catch (Exception ex)
        {
            if (i == maxRetries - 1)
            {
                Log.Fatal(ex, "An error occurred while migrating the database after {MaxRetries} attempts", maxRetries);
                throw;
            }
            Log.Warning(ex, "Migration attempt {Attempt} failed, retrying...", i + 1);
            Thread.Sleep(delay);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Catalog API V1");
    c.RoutePrefix = "swagger";  
});

app.UseExceptionHandlingMiddleware();

//app.UseHttpsRedirection();

app.UseCors();

app.UseSerilogRequestLogging();

app.UseAuthorization();

app.MapGrpcService<HotelCatalog.Api.Grpc.GrpcCatalogService>();
app.MapGrpcHealthChecksService();

app.MapControllers();

try
{
    Log.Information("Starting Hotel Catalog API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}