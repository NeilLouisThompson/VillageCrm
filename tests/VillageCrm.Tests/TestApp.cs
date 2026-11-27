using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VillageCrm.Data;
using VillageCrm.Data.Mock;
using VillageCrm.Integrations;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public sealed class TestClock(DateTime localNow) : TimeProvider
{
    public DateTime Now { get; set; } = localNow;

    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Builds the real service graph on an empty mock store with a fixed clock.</summary>
public sealed class TestApp
{
    // Wednesday 4 November 2026, 09:00.
    public static readonly DateTime DefaultNow = new(2026, 11, 4, 9, 0, 0);

    public IServiceProvider Services { get; }
    public TestClock Clock { get; }

    public TestApp(Action<VillageOptions>? configure = null)
    {
        Clock = new TestClock(DefaultNow);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddMockDataLayer(seed: false);
        services.AddMockIntegrations();
        services.AddVillageServices(new ConfigurationBuilder().Build());
        services.Configure<VillageOptions>(o =>
        {
            o.JobCategories = ["Plumbing", "Electrical"];
            o.ScheduleWindows = ["08:00 - 12:00"];
            o.ComplianceReminderDays = [30, 14, 7, 1];
            configure?.Invoke(o);
        });
        Services = services.BuildServiceProvider();
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public IRepository<T> Repo<T>() where T : class, IEntity => Get<IRepository<T>>();

    public async Task<Resident> AddResidentAsync(string name = "Test Resident", string dietary = "Soft food", bool dataSharing = false) =>
        await Get<ResidentService>().AddAsync(new Resident
        {
            FullName = name, Unit = "A-01", DietaryFlag = dietary, DataSharingConsent = dataSharing
        }, "Tester");
}
