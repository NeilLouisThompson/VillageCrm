using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Services;

/// <summary>What the kitchen may see: dietary flag only (and only with consent), never diagnoses.</summary>
public record KitchenLine(int BookingId, string Name, string Unit, int Guests, string DietaryFlag, BookingStatus Status);

public record WelfareAlert(Resident Resident, DateTime? LastMeal);

public class RestaurantService(
    IRepository<Sitting> sittings,
    IRepository<MenuEntry> menus,
    IRepository<MealBooking> bookings,
    IRepository<Resident> residents,
    IOptions<VillageOptions> options,
    TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Sitting>> AllSittingsAsync() =>
        (await sittings.ListAsync()).OrderBy(s => s.EventDate ?? DateTime.MinValue).ThenBy(s => s.StartsAt).ToList();

    public async Task<List<Sitting>> SittingsForAsync(DateTime date) =>
        (await sittings.ListAsync())
            .Where(s => s.Active && (s.EventDate is null || s.EventDate.Value.Date == date.Date))
            .OrderBy(s => s.StartsAt)
            .ToList();

    public static DateTime StartOf(Sitting sitting, DateTime date) => date.Date.Add(sitting.StartsAt.ToTimeSpan());

    public static DateTime CutOff(Sitting sitting, DateTime date) => StartOf(sitting, date).AddMinutes(-sitting.CutOffMinutes);

    public bool IsOpen(Sitting sitting, DateTime date) => Now < CutOff(sitting, date);

    public async Task<int> CoversAsync(DateTime date, int sittingId) =>
        (await ActiveBookingsAsync(date, sittingId)).Sum(b => 1 + b.Guests);

    public async Task<MealBooking> BookAsync(int residentId, DateTime date, int sittingId, int guests, string bookedBy, bool overrideCutOff = false)
    {
        var sitting = await sittings.GetAsync(sittingId) ?? throw new ArgumentException("Unknown meal.");
        if (!sitting.Active || (sitting.EventDate is { } eventDate && eventDate.Date != date.Date))
        {
            throw new ArgumentException($"{sitting.Name} is not served on {Format.Date(date)}.");
        }
        if (guests < 0 || guests > options.Value.MaxGuestsPerBooking)
        {
            throw new ArgumentException($"You can bring up to {options.Value.MaxGuestsPerBooking} guests.");
        }
        if (!overrideCutOff && !IsOpen(sitting, date))
        {
            throw new InvalidOperationException($"Bookings for {sitting.Name} closed at {CutOff(sitting, date):HH:mm} on {Format.Date(date)}. Please call reception.");
        }

        var resident = await residents.GetAsync(residentId) ?? throw new ArgumentException("Unknown resident.");
        if (!resident.IsActive)
        {
            throw new InvalidOperationException("This resident is no longer living in the village.");
        }

        var existing = await ActiveBookingsAsync(date, sittingId);
        if (existing.Any(b => b.ResidentId == residentId))
        {
            throw new InvalidOperationException("You are already booked for this meal.");
        }
        if (existing.Sum(b => 1 + b.Guests) + 1 + guests > sitting.Capacity)
        {
            throw new InvalidOperationException($"Sorry, {sitting.Name} is full.");
        }

        return await bookings.AddAsync(new MealBooking
        {
            ResidentId = resident.Id,
            ResidentName = resident.FullName,
            Unit = resident.Unit,
            Date = date.Date,
            SittingId = sitting.Id,
            SittingName = sitting.Name,
            Guests = guests,
            Status = BookingStatus.Booked,
            BookedBy = bookedBy,
            BookedAt = Now
        });
    }

    /// <summary>Pass residentId when residents cancel for themselves; staff with override may cancel after the cut-off.</summary>
    public async Task CancelAsync(int bookingId, int? residentId = null, bool overrideCutOff = false)
    {
        var booking = await bookings.GetAsync(bookingId) ?? throw new ArgumentException("Unknown booking.");
        if (residentId is not null && booking.ResidentId != residentId)
        {
            throw new UnauthorizedAccessException("This is not your booking.");
        }
        if (booking.Status != BookingStatus.Booked)
        {
            throw new InvalidOperationException("Only open bookings can be cancelled.");
        }
        var sitting = await sittings.GetAsync(booking.SittingId) ?? throw new ArgumentException("Unknown meal.");
        if (!overrideCutOff && !IsOpen(sitting, booking.Date))
        {
            throw new InvalidOperationException("It is too late to cancel this meal. Please call reception.");
        }
        booking.Status = BookingStatus.Cancelled;
        await bookings.UpdateAsync(booking);
    }

    public async Task MarkAttendanceAsync(int bookingId, bool attended)
    {
        var booking = await bookings.GetAsync(bookingId) ?? throw new ArgumentException("Unknown booking.");
        if (booking.Status == BookingStatus.Cancelled)
        {
            throw new InvalidOperationException("This booking was cancelled.");
        }
        booking.Status = attended ? BookingStatus.Attended : BookingStatus.NoShow;
        await bookings.UpdateAsync(booking);
    }

    public async Task<List<MealBooking>> BookingsForResidentAsync(int residentId, DateTime from, DateTime to) =>
        (await bookings.ListAsync(b => b.ResidentId == residentId && b.Status != BookingStatus.Cancelled))
            .Where(b => b.Date >= from.Date && b.Date <= to.Date)
            .OrderBy(b => b.Date)
            .ToList();

    /// <summary>Seats taken (residents plus guests) per day.</summary>
    public async Task<Dictionary<DateTime, int>> CoversByDayAsync(DateTime from, DateTime to) =>
        (await bookings.ListAsync(b => b.Status != BookingStatus.Cancelled))
            .Where(b => b.Date >= from.Date && b.Date <= to.Date)
            .GroupBy(b => b.Date.Date)
            .ToDictionary(g => g.Key, g => g.Sum(b => 1 + b.Guests));

    public async Task<List<KitchenLine>> KitchenListAsync(DateTime date, int sittingId)
    {
        var people = (await residents.ListAsync()).ToDictionary(r => r.Id);
        return (await ActiveBookingsAsync(date, sittingId))
            .OrderBy(b => b.ResidentName)
            .Select(b => new KitchenLine(b.Id, b.ResidentName, b.Unit, b.Guests,
                people.TryGetValue(b.ResidentId, out var r) && r.DataSharingConsent ? r.DietaryFlag : "",
                b.Status))
            .ToList();
    }

    /// <summary>Consenting residents in the village with no attended meal in the last N days.</summary>
    public async Task<List<WelfareAlert>> WelfareAlertsAsync()
    {
        var since = Now.Date.AddDays(-options.Value.WelfareNoMealDays);
        var attended = await bookings.ListAsync(b => b.Status == BookingStatus.Attended);
        return (await residents.ListAsync())
            .Where(r => r.WelfareMonitoringConsent && r.Status == ResidentStatus.InVillage)
            .Select(r => new WelfareAlert(r, attended.Where(b => b.ResidentId == r.Id).Select(b => (DateTime?)b.Date).Max()))
            .Where(a => a.LastMeal is null || a.LastMeal < since)
            .OrderBy(a => a.Resident.FullName)
            .ToList();
    }

    public async Task<MenuEntry?> MenuAsync(DateTime date, int sittingId)
    {
        var day = date.Date;
        return (await menus.ListAsync(m => m.Date == day && m.SittingId == sittingId)).FirstOrDefault();
    }

    public async Task SaveMenuAsync(DateTime date, int sittingId, string dishes, string dietaryOptions, UserRole role)
    {
        EnsureCanManage(role);
        var menu = await MenuAsync(date, sittingId);
        if (menu is null)
        {
            await menus.AddAsync(new MenuEntry { Date = date.Date, SittingId = sittingId, Dishes = dishes.Trim(), DietaryOptions = dietaryOptions.Trim() });
            return;
        }
        menu.Dishes = dishes.Trim();
        menu.DietaryOptions = dietaryOptions.Trim();
        await menus.UpdateAsync(menu);
    }

    /// <summary>Adds or updates a sitting or special event. Supervisors set cut-off and capacity here.</summary>
    public async Task SaveSittingAsync(Sitting sitting, UserRole role)
    {
        EnsureCanManage(role);
        if (string.IsNullOrWhiteSpace(sitting.Name) || sitting.Capacity <= 0 || sitting.CutOffMinutes < 0 || sitting.Price < 0)
        {
            throw new ArgumentException("Give a name, seats above zero, and a cut-off and price that are not negative.");
        }
        sitting.Name = sitting.Name.Trim();
        if (sitting.Id == 0)
        {
            await sittings.AddAsync(sitting);
        }
        else
        {
            await sittings.UpdateAsync(sitting);
        }
    }

    private async Task<List<MealBooking>> ActiveBookingsAsync(DateTime date, int sittingId)
    {
        var day = date.Date;
        return await bookings.ListAsync(b => b.Date == day && b.SittingId == sittingId && b.Status != BookingStatus.Cancelled);
    }

    private static void EnsureCanManage(UserRole role)
    {
        if (!Access.Allows(role, Area.ManageRestaurant))
        {
            throw new UnauthorizedAccessException($"Only {Access.WhoMay(Area.ManageRestaurant)} can change menus and meal settings.");
        }
    }
}
