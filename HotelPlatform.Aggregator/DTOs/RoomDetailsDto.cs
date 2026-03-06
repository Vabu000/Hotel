namespace HotelPlatform.Aggregator.DTOs;

public class RoomDetailsDto
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal Price { get; set; }

    public RoomCategoryInfo? Category { get; set; }

    public List<ReviewInfo> Reviews { get; set; } = new();
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }

    public List<ReservationInfo> Reservations { get; set; } = new();
    public bool IsAvailable { get; set; }
}

public class RoomCategoryInfo
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ServiceInfo> Services { get; set; } = new();
}

public class ServiceInfo
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ReviewInfo
{
    public string Id { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsVerified { get; set; }
    public bool IsApproved { get; set; }
}

public class ReservationInfo
{
    public int ReservationId { get; set; }
    public int ClientId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
}