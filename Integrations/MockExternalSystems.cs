using System.Text.Json;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Integrations;

public record PastelPosting(string Ledger, DateTime Date, string Reference, string Account, string Description, decimal Amount, string PostedBy);

public record PilotCustomerRecord(PilotCustomer Customer, DateTime ReceivedAt);

public record PilotTillSale(string Reference, string ItemCode, string ItemName, StockStore Store, decimal Quantity);

/// <summary>
/// Demo only: what the mock Pastel and Pilot "hold", built from the messages they have received.
/// Remove together with the mock gateways when the real systems are connected.
/// </summary>
public class MockExternalSystems(
    IRepository<OutboxMessage> outbox,
    IRepository<Resident> residents,
    IRepository<MealBooking> bookings,
    IRepository<Sitting> sittings,
    IRepository<StockItem> stock,
    IPilotGateway pilot)
{
    public async Task<List<PastelPosting>> PastelPostingsAsync()
    {
        var postings = (await outbox.ListAsync(m => m.Target == OutboxTarget.Pastel && m.Status == OutboxStatus.Sent))
            .Select(ToPosting).ToList();

        // Pilot posts dining room charges to Pastel itself.
        var accounts = (await residents.ListAsync()).ToDictionary(r => r.Id, r => r.AccountNumber);
        var prices = (await sittings.ListAsync()).ToDictionary(s => s.Id, s => s.Price);
        postings.AddRange((await bookings.ListAsync(b => b.Status == BookingStatus.Attended)).Select(b => new PastelPosting(
            "Debtors", b.Date, $"PIL-MEAL-{b.Id}", accounts.GetValueOrDefault(b.ResidentId, ""),
            $"Dining room: {b.SittingName}" + (b.Guests > 0 ? $" + {b.Guests} guest(s)" : ""),
            prices.GetValueOrDefault(b.SittingId) * (1 + b.Guests), "Pilot")));

        return postings.OrderByDescending(p => p.Date).ThenBy(p => p.Reference).ToList();
    }

    public async Task<List<PilotCustomerRecord>> PilotCustomersAsync() =>
        (await outbox.ListAsync(m => m.Target == OutboxTarget.Pilot && m.Type == "CustomerAccount" && m.Status == OutboxStatus.Sent))
            .OrderBy(m => m.SentAt).ThenBy(m => m.Id)
            .Select(m => new PilotCustomerRecord(JsonSerializer.Deserialize<PilotCustomer>(m.Payload)!, m.SentAt ?? m.CreatedAt))
            .GroupBy(r => r.Customer.AccountNumber)
            .Select(g => g.Last())
            .OrderBy(r => r.Customer.Name)
            .ToList();

    public async Task<List<PilotTillSale>> PilotTillSalesAsync(DateTime day)
    {
        var items = (await stock.ListAsync()).ToDictionary(i => i.Code);
        return (await pilot.GetSalesAsync(day))
            .Select(s => items.TryGetValue(s.ItemCode, out var item)
                ? new PilotTillSale(s.Reference, s.ItemCode, item.Name, item.Store, s.Quantity)
                : new PilotTillSale(s.Reference, s.ItemCode, s.ItemCode, StockStore.Bar, s.Quantity))
            .OrderBy(s => s.Store).ThenBy(s => s.ItemName)
            .ToList();
    }

    private static PastelPosting ToPosting(OutboxMessage m)
    {
        var p = JsonDocument.Parse(m.Payload).RootElement;
        string Text(string name) => p.TryGetProperty(name, out var v) ? v.ToString() : "";
        decimal Money(string name) => p.TryGetProperty(name, out var v) && v.TryGetDecimal(out var d) ? d : 0;
        var date = m.SentAt ?? m.CreatedAt;

        return m.Type switch
        {
            "DebtorCharge" => new("Debtors", date, m.Reference, Text("AccountNumber"), Text("Description"), Money("Amount"), "Village Care"),
            "SupplierInvoice" => new("Creditors", date, m.Reference, Text("Supplier"),
                $"Invoice {Text("InvoiceNumber")} for order {Text("PoNumber")}", Money("Amount"), "Village Care"),
            "ProjectCostJournal" => new("General ledger", date, m.Reference, Text("CostCentre"),
                $"{Text("Project")}: {Text("Kind")} (retention {Money("Retention"):N2})", Money("Amount"), "Village Care"),
            _ => new("Other", date, m.Reference, "", m.Type, 0, "Village Care")
        };
    }
}
