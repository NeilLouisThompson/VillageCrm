using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Integrations;

public class MockPastelGateway(
    IOptionsMonitor<VillageOptions> options,
    ILogger<MockPastelGateway> logger,
    TimeProvider time,
    IRepository<Resident> residents,
    IRepository<MealBooking> bookings,
    IRepository<Sitting> sittings) : IPastelGateway
{
    private readonly ConcurrentBag<(string Account, StatementLine Line)> _postedCharges = new();

    public Task SendAsync(OutboxMessage message)
    {
        if (options.CurrentValue.SimulatePastelLocked)
        {
            throw new IntegrationUnavailableException("Pastel company is locked (simulated month-end).");
        }
        if (message.Type == "DebtorCharge")
        {
            var payload = JsonDocument.Parse(message.Payload).RootElement;
            _postedCharges.Add((payload.GetProperty("AccountNumber").GetString()!, new StatementLine(
                time.GetLocalNow().DateTime, payload.GetProperty("Description").GetString()!, message.Reference,
                payload.GetProperty("Amount").GetDecimal())));
        }
        logger.LogInformation("Mock Pastel received {Type} {Reference}", message.Type, message.Reference);
        return Task.CompletedTask;
    }

    // Fake but stable levies and payments, plus job charges actually posted, plus meals attended
    // (in reality Pilot posts meal charges to Pastel; the mock reads bookings to simulate that).
    public async Task<AccountStatement> GetStatementAsync(string accountNumber)
    {
        var seed = accountNumber.Sum(c => c);
        var levy = 3_500m + seed % 10 * 100;
        var month = new DateTime(time.GetLocalNow().Year, time.GetLocalNow().Month, 1);
        var lines = new List<StatementLine>();
        for (var i = 2; i >= 0; i--)
        {
            var start = month.AddMonths(-i);
            lines.Add(new StatementLine(start, "Monthly levy", $"LEV-{start:yyyyMM}", levy));
            if (i > 0)
            {
                lines.Add(new StatementLine(start.AddDays(4), "Payment received - thank you", $"PAY-{start:yyyyMM}", -levy));
            }
        }

        var resident = (await residents.ListAsync(r => r.AccountNumber == accountNumber)).FirstOrDefault();
        if (resident is not null)
        {
            var prices = (await sittings.ListAsync()).ToDictionary(s => s.Id, s => s.Price);
            foreach (var meal in await bookings.ListAsync(b => b.ResidentId == resident.Id && b.Status == BookingStatus.Attended))
            {
                var guests = meal.Guests > 0 ? $" + {meal.Guests} guest(s)" : "";
                lines.Add(new StatementLine(meal.Date, $"Dining room: {meal.SittingName}{guests} (Pilot)", $"PIL-MEAL-{meal.Id}",
                    prices.GetValueOrDefault(meal.SittingId) * (1 + meal.Guests)));
            }
        }
        lines.AddRange(_postedCharges.Where(c => c.Account == accountNumber).Select(c => c.Line));

        var ordered = lines.OrderBy(l => l.Date).ToList();
        return new AccountStatement(accountNumber, ordered.Sum(l => l.Amount), ordered);
    }
}

public class MockPilotGateway(ILogger<MockPilotGateway> logger, IRepository<StockItem> stock) : IPilotGateway
{
    public Task SendAsync(OutboxMessage message)
    {
        logger.LogInformation("Mock Pilot received {Type} {Reference}", message.Type, message.Reference);
        return Task.CompletedTask;
    }

    // Fake but stable: a few units of each bar and shop item per day.
    public async Task<IReadOnlyList<PilotSale>> GetSalesAsync(DateTime day) =>
        (await stock.ListAsync(i => i.Store != StockStore.Maintenance))
            .Select(i => new PilotSale($"PILOT-{day:yyyyMMdd}-{i.Code}", i.Code, 1 + (i.Code.Sum(c => c) + day.Day) % 3))
            .ToList();
}

public static class MockIntegrationExtensions
{
    /// <summary>Registers fake Pastel and Pilot connections. Replace with real gateways later.</summary>
    public static IServiceCollection AddMockIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IPastelGateway, MockPastelGateway>();
        services.AddSingleton<IPilotGateway, MockPilotGateway>();
        return services;
    }
}
