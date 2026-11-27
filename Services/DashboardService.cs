using System.Text;
using VillageCrm.Models;

namespace VillageCrm.Services;

public enum Rag { None, Green, Amber, Red }

public record DashboardRow(string Area, string Measure, string Value, Rag Rag = Rag.None);

public class DashboardService(
    ResidentService residents,
    JobCardService jobs,
    MaintenanceService maintenance,
    ProjectService projects,
    StockService stock,
    ComplianceService compliance,
    PolicyService policies,
    RestaurantService restaurant,
    TimeProvider time)
{
    public async Task<List<DashboardRow>> GetAsync()
    {
        var today = time.GetLocalNow().Date;
        var rows = new List<DashboardRow>();

        var census = await residents.GetCensusAsync();
        rows.Add(new("Occupancy", "Active residents", census.Residents.Count.ToString()));
        rows.Add(new("Occupancy", "In the village today", census.InVillage.ToString()));
        rows.Add(new("Occupancy", "Away", census.Away.ToString()));
        rows.Add(new("Occupancy", "In hospital", census.Hospital.ToString(), census.Hospital > 0 ? Rag.Amber : Rag.Green));
        rows.Add(new("Occupancy", "Frail care", census.FrailCare.ToString()));

        var allJobs = await jobs.ListAsync();
        var open = allJobs.Where(j => j.Status is not (JobStatus.Done or JobStatus.Closed)).ToList();
        var breached = open.Count(jobs.IsSlaBreached);
        var finished = allJobs.Where(j => j.CompletedAt is not null).ToList();
        var withinSla = finished.Count == 0 ? 1 : (double)finished.Count(j => j.CompletedAt <= j.SlaDueAt) / finished.Count;
        rows.Add(new("Job cards", "Open", open.Count.ToString()));
        rows.Add(new("Job cards", "Past service time (escalated)", breached.ToString(), breached > 0 ? Rag.Red : Rag.Green));
        rows.Add(new("Job cards", "Finished within service time", $"{withinSla:P0}", withinSla >= 0.9 ? Rag.Green : withinSla >= 0.75 ? Rag.Amber : Rag.Red));
        rows.Add(new("Job cards", "Waiting for resident to confirm", allJobs.Count(j => j.Status == JobStatus.Done).ToString()));

        var workOrders = await maintenance.WorkOrdersAsync(openOnly: true);
        var lateOrders = workOrders.Count(w => w.DueDate.Date < today);
        rows.Add(new("Maintenance", "Open work orders", workOrders.Count.ToString()));
        rows.Add(new("Maintenance", "Overdue work orders", lateOrders.ToString(), lateOrders > 0 ? Rag.Red : Rag.Green));

        foreach (var p in (await projects.ListAsync()).Where(p => p.Status != ProjectStatus.Closed))
        {
            var used = p.RevisedBudget == 0 ? 0 : p.Actual / p.RevisedBudget;
            rows.Add(new("Projects", p.Name, $"{Format.Money(p.Actual)} of {Format.Money(p.RevisedBudget)}",
                used > 1 ? Rag.Red : used > 0.9m ? Rag.Amber : Rag.Green));
        }

        var items = await stock.ListAsync();
        var reorder = items.Count(i => i.NeedsReorder);
        rows.Add(new("Stock", "Stock value", Format.Money(items.Sum(i => i.Value))));
        rows.Add(new("Stock", "Items to reorder", reorder.ToString(), reorder > 0 ? Rag.Amber : Rag.Green));

        var seatsToday = (await restaurant.CoversByDayAsync(today, today)).GetValueOrDefault(today);
        var welfare = (await restaurant.WelfareAlertsAsync()).Count;
        rows.Add(new("Restaurant", "Seats booked today", seatsToday.ToString()));
        rows.Add(new("Restaurant", "Welfare alerts (missed meals)", welfare.ToString(), welfare > 0 ? Rag.Red : Rag.Green));

        var obligations = await compliance.ListAsync(includeClosed: false);
        var overdue = obligations.Count(c => compliance.StateOf(c) == ComplianceState.Overdue);
        var dueSoon = obligations.Count(c => compliance.StateOf(c) is ComplianceState.DueSoon or ComplianceState.Acknowledged);
        rows.Add(new("Compliance", "Overdue", overdue.ToString(), overdue > 0 ? Rag.Red : Rag.Green));
        rows.Add(new("Compliance", "Due soon", dueSoon.ToString(), dueSoon > 0 ? Rag.Amber : Rag.Green));
        rows.Add(new("Compliance", "On track", obligations.Count(c => compliance.StateOf(c) == ComplianceState.OnTrack).ToString(), Rag.Green));

        foreach (var policy in (await policies.ListAsync(residentView: false)).Where(p => p.Current is not null))
        {
            var rate = PolicyService.AcknowledgementRate(policy, await policies.TargetCountAsync(policy));
            rows.Add(new("Policies", $"{policy.Title} - read and acknowledged", $"{rate:P0}",
                rate >= 0.9 ? Rag.Green : rate >= 0.5 ? Rag.Amber : Rag.Red));
        }

        return rows;
    }

    public static string ToCsv(IEnumerable<DashboardRow> rows)
    {
        var csv = new StringBuilder("Area,Measure,Value,Status\r\n");
        foreach (var r in rows)
        {
            csv.Append($"{Escape(r.Area)},{Escape(r.Measure)},{Escape(r.Value)},{(r.Rag == Rag.None ? "" : r.Rag.ToString())}\r\n");
        }
        return csv.ToString();
    }

    // Quotes values and neutralises spreadsheet formulas (CSV injection).
    private static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
