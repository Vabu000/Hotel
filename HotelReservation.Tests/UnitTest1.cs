using HotelReservation.Domain.Entities;

namespace HotelReservation.Tests;

public class ReservationTests
{
    [Fact]
    public void NewReservation_InitializesPaymentsCollection()
    {
        var reservation = new Reservation();

        Assert.NotNull(reservation.Payments);
        Assert.Empty(reservation.Payments);
    }

    [Theory]
    [InlineData(ReservationStatus.Pending, 0)]
    [InlineData(ReservationStatus.Confirmed, 1)]
    [InlineData(ReservationStatus.CheckedIn, 2)]
    [InlineData(ReservationStatus.CheckedOut, 3)]
    [InlineData(ReservationStatus.Cancelled, 4)]
    public void ReservationStatus_UsesStableDatabaseValues(ReservationStatus status, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)status);
    }
}
