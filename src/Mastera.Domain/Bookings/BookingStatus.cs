namespace Mastera.Domain.Bookings;

public enum BookingStatus
{
    /// <summary>Requested, waiting for the salon to confirm.</summary>
    Pending,

    Confirmed,

    Completed,

    NoShow,

    CancelledByClient,

    CancelledBySalon,
}

public enum BookingSource
{
    /// <summary>Booked by the client on the salon's booking page.</summary>
    Online,

    /// <summary>Entered by staff, e.g. after a phone call or an Instagram message.</summary>
    Manual,

    /// <summary>A client who walked in.</summary>
    WalkIn,
}

public static class BookingStatuses
{
    /// <summary>Pending and confirmed bookings take up the master's time; the others don't.</summary>
    public static bool IsActive(this BookingStatus status) => status is BookingStatus.Pending or BookingStatus.Confirmed;
}
