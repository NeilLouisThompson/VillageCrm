using VillageCrm.Integrations;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class RestaurantTests
{
    // TestApp clock: Wednesday 4 Nov 2026, 09:00.
    private static readonly DateTime Today = TestApp.DefaultNow.Date;

    private static async Task<Sitting> AddSittingAsync(TestApp app, string name = "Lunch", int capacity = 10, int cutOffMinutes = 150, DateTime? eventDate = null, decimal price = 85)
    {
        var sitting = new Sitting { Name = name, StartsAt = new TimeOnly(12, 30), Capacity = capacity, CutOffMinutes = cutOffMinutes, Price = price, EventDate = eventDate };
        await app.Get<RestaurantService>().SaveSittingAsync(sitting, UserRole.RestaurantSupervisor);
        return sitting;
    }

    [Fact]
    public async Task Booking_is_open_until_the_cut_off_then_only_supervisors_can_book()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var lunch = await AddSittingAsync(app, cutOffMinutes: 150); // closes 10:00
        var restaurant = app.Get<RestaurantService>();

        await restaurant.BookAsync(resident.Id, Today, lunch.Id, 0, "Resident");

        app.Clock.Now = Today.AddHours(10).AddMinutes(1);
        var other = await app.AddResidentAsync("Late");
        await Assert.ThrowsAsync<InvalidOperationException>(() => restaurant.BookAsync(other.Id, Today, lunch.Id, 0, "Late"));
        await restaurant.BookAsync(other.Id, Today, lunch.Id, 0, "Marietjie", overrideCutOff: true);

        Assert.Equal(2, await restaurant.CoversAsync(Today, lunch.Id));
    }

    [Fact]
    public async Task Capacity_counts_guests_and_double_booking_is_refused()
    {
        var app = new TestApp();
        var a = await app.AddResidentAsync("A");
        var b = await app.AddResidentAsync("B");
        var lunch = await AddSittingAsync(app, capacity: 4);
        var restaurant = app.Get<RestaurantService>();

        await restaurant.BookAsync(a.Id, Today, lunch.Id, 2, "A");
        await Assert.ThrowsAsync<InvalidOperationException>(() => restaurant.BookAsync(a.Id, Today, lunch.Id, 0, "A"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restaurant.BookAsync(b.Id, Today, lunch.Id, 1, "B"));
        await restaurant.BookAsync(b.Id, Today, lunch.Id, 0, "B");
    }

    [Fact]
    public async Task Too_many_guests_is_refused()
    {
        var app = new TestApp(o => o.MaxGuestsPerBooking = 2);
        var resident = await app.AddResidentAsync();
        var lunch = await AddSittingAsync(app);

        await Assert.ThrowsAsync<ArgumentException>(() => app.Get<RestaurantService>().BookAsync(resident.Id, Today, lunch.Id, 3, "R"));
    }

    [Fact]
    public async Task Residents_cancel_only_their_own_booking_and_only_before_the_cut_off()
    {
        var app = new TestApp();
        var owner = await app.AddResidentAsync("Owner");
        var other = await app.AddResidentAsync("Other");
        var lunch = await AddSittingAsync(app);
        var restaurant = app.Get<RestaurantService>();
        var booking = await restaurant.BookAsync(owner.Id, Today, lunch.Id, 0, "Owner");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => restaurant.CancelAsync(booking.Id, other.Id));

        app.Clock.Now = Today.AddHours(11);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restaurant.CancelAsync(booking.Id, owner.Id));
        await restaurant.CancelAsync(booking.Id, overrideCutOff: true);

        Assert.Equal(0, await restaurant.CoversAsync(Today, lunch.Id));
    }

    [Fact]
    public async Task Special_event_is_served_only_on_its_date()
    {
        var app = new TestApp();
        var christmas = new DateTime(2026, 12, 25);
        var resident = await app.AddResidentAsync();
        await AddSittingAsync(app);
        var feast = await AddSittingAsync(app, "Christmas lunch", eventDate: christmas, price: 350);
        var restaurant = app.Get<RestaurantService>();

        Assert.DoesNotContain(await restaurant.SittingsForAsync(Today), s => s.Id == feast.Id);
        Assert.Contains(await restaurant.SittingsForAsync(christmas), s => s.Id == feast.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => restaurant.BookAsync(resident.Id, Today, feast.Id, 0, "R"));
        await restaurant.BookAsync(resident.Id, christmas, feast.Id, 1, "R");
    }

    [Fact]
    public async Task Only_supervisors_and_manager_change_meal_settings_and_menus()
    {
        var app = new TestApp();
        var restaurant = app.Get<RestaurantService>();
        var lunch = await AddSittingAsync(app);

        lunch.CutOffMinutes = 24 * 60;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => restaurant.SaveSittingAsync(lunch, UserRole.Reception));
        await restaurant.SaveSittingAsync(lunch, UserRole.Manager);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => restaurant.SaveMenuAsync(Today, lunch.Id, "Soup", "", UserRole.Resident));
        await restaurant.SaveMenuAsync(Today, lunch.Id, "Soup", "Soft food", UserRole.RestaurantSupervisor);

        Assert.Equal(24 * 60, (await app.Repo<Sitting>().GetAsync(lunch.Id))!.CutOffMinutes);
        Assert.Equal("Soup", (await restaurant.MenuAsync(Today, lunch.Id))!.Dishes);
    }

    [Fact]
    public async Task Kitchen_list_shows_dietary_flag_only_with_consent_and_never_medical_data()
    {
        var app = new TestApp();
        var shares = await app.AddResidentAsync("Shares", "Nut allergy", dataSharing: true);
        var private_ = await app.AddResidentAsync("Private", "Diabetic diet", dataSharing: false);
        await app.Get<ResidentService>().SaveMedicalAsync(new MedicalRecord { ResidentId = shares.Id, Conditions = "Secret diagnosis" }, UserRole.Clinical, "Sister");
        var lunch = await AddSittingAsync(app);
        var restaurant = app.Get<RestaurantService>();
        await restaurant.BookAsync(shares.Id, Today, lunch.Id, 0, "S");
        await restaurant.BookAsync(private_.Id, Today, lunch.Id, 0, "P");

        var list = await restaurant.KitchenListAsync(Today, lunch.Id);

        Assert.Equal("Nut allergy", list.Single(l => l.Name == "Shares").DietaryFlag);
        Assert.Equal("", list.Single(l => l.Name == "Private").DietaryFlag);
        Assert.DoesNotContain(list, l => l.DietaryFlag.Contains("Secret"));
    }

    [Fact]
    public async Task Welfare_alert_only_for_consenting_residents_who_missed_meals()
    {
        var app = new TestApp(o => o.WelfareNoMealDays = 2);
        var residents = app.Get<ResidentService>();
        var missed = await app.AddResidentAsync("Missed");
        var ate = await app.AddResidentAsync("Ate");
        var noConsent = await app.AddResidentAsync("NoConsent");
        await residents.UpdateConsentAsync(missed.Id, false, false, false, true, "Tester");
        await residents.UpdateConsentAsync(ate.Id, false, false, false, true, "Tester");
        var lunch = await AddSittingAsync(app);
        var restaurant = app.Get<RestaurantService>();

        app.Clock.Now = Today.AddDays(-4).AddHours(9);
        var old = await restaurant.BookAsync(missed.Id, Today.AddDays(-4), lunch.Id, 0, "M");
        app.Clock.Now = Today.AddDays(-1).AddHours(9);
        var recent = await restaurant.BookAsync(ate.Id, Today.AddDays(-1), lunch.Id, 0, "A");
        await restaurant.MarkAttendanceAsync(old.Id, true);
        await restaurant.MarkAttendanceAsync(recent.Id, true);
        app.Clock.Now = TestApp.DefaultNow;

        var alerts = await restaurant.WelfareAlertsAsync();

        var alert = Assert.Single(alerts);
        Assert.Equal(missed.Id, alert.Resident.Id);
        Assert.Equal(Today.AddDays(-4), alert.LastMeal);
        Assert.DoesNotContain(alerts, a => a.Resident.Id == noConsent.Id);
    }

    [Fact]
    public async Task Attended_meals_appear_on_the_statement()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var lunch = await AddSittingAsync(app, price: 85);
        var restaurant = app.Get<RestaurantService>();
        var booking = await restaurant.BookAsync(resident.Id, Today, lunch.Id, 1, "R");
        var pastel = app.Get<IPastelGateway>();

        Assert.DoesNotContain((await pastel.GetStatementAsync(resident.AccountNumber)).Lines, l => l.Reference == $"PIL-MEAL-{booking.Id}");

        await restaurant.MarkAttendanceAsync(booking.Id, true);

        var line = Assert.Single((await pastel.GetStatementAsync(resident.AccountNumber)).Lines, l => l.Reference == $"PIL-MEAL-{booking.Id}");
        Assert.Equal(170, line.Amount);
    }

    [Fact]
    public async Task Bar_and_shop_stock_are_separate_and_Pilot_sales_import_once()
    {
        var app = new TestApp();
        var stock = app.Get<StockService>();
        await stock.AddItemAsync(new StockItem { Store = StockStore.Maintenance, Code = "PL-1", Name = "Washer", OnHand = 10, Max = 20 });
        var beer = await stock.AddItemAsync(new StockItem { Store = StockStore.Bar, Code = "BAR-1", Name = "Lager", OnHand = 20, Max = 50 });
        await stock.AddItemAsync(new StockItem { Store = StockStore.Shop, Code = "SHP-1", Name = "Milk", OnHand = 10, Max = 20 });

        Assert.Equal(["Lager"], (await stock.ListAsync(StockStore.Bar)).Select(i => i.Name));
        Assert.Equal(3, (await stock.ListAsync()).Count);

        Assert.Equal(2, await stock.ImportPilotSalesAsync(Today, "Marietjie"));
        Assert.Equal(0, await stock.ImportPilotSalesAsync(Today, "Marietjie"));

        var saved = (await app.Repo<StockItem>().GetAsync(beer.Id))!;
        Assert.True(saved.OnHand < 20);
        Assert.Equal(10, (await stock.ListAsync(StockStore.Maintenance)).Single().OnHand);
    }

    [Fact]
    public void Restaurant_supervisor_sees_bar_and_shop_only()
    {
        Assert.Equal([StockStore.Bar, StockStore.Shop], Access.StoresFor(UserRole.RestaurantSupervisor));
        Assert.Equal([StockStore.Maintenance], Access.StoresFor(UserRole.Maintenance));
        Assert.True(Access.Allows(UserRole.RestaurantSupervisor, Area.ManageRestaurant));
        Assert.False(Access.Allows(UserRole.Reception, Area.ManageRestaurant));
        Assert.False(Access.Allows(UserRole.RestaurantSupervisor, Area.Medical));
    }
}
