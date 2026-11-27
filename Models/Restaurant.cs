namespace VillageCrm.Models;

/// <summary>A meal service. Regular sittings run every day; a sitting with an EventDate is a one-off special event.</summary>
public class Sitting : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TimeOnly StartsAt { get; set; }
    public int Capacity { get; set; }

    /// <summary>Bookings close this many minutes before the meal starts. Set by restaurant supervisors.</summary>
    public int CutOffMinutes { get; set; }
    public decimal Price { get; set; }
    public DateTime? EventDate { get; set; }
    public bool Active { get; set; } = true;

    public bool IsSpecialEvent => EventDate is not null;
}

public class MenuEntry : IEntity
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int SittingId { get; set; }
    public string Dishes { get; set; } = "";
    public string DietaryOptions { get; set; } = "";
}

public enum BookingStatus { Booked, Cancelled, Attended, NoShow }

public class MealBooking : IEntity
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string Unit { get; set; } = "";
    public DateTime Date { get; set; }
    public int SittingId { get; set; }
    public string SittingName { get; set; } = "";
    public int Guests { get; set; }
    public BookingStatus Status { get; set; }
    public string BookedBy { get; set; } = "";
    public DateTime BookedAt { get; set; }
}
