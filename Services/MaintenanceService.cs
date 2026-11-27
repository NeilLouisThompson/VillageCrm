using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class MaintenanceService(IRepository<Asset> assets, IRepository<WorkOrder> workOrders, TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Asset>> AssetsAsync() => (await assets.ListAsync()).OrderBy(a => a.Name).ToList();

    public async Task<List<WorkOrder>> WorkOrdersAsync(bool openOnly) =>
        (await workOrders.ListAsync(w => !openOnly || w.Status == WorkOrderStatus.Open))
            .OrderBy(w => w.DueDate).ToList();

    public async Task<Asset> AddAssetAsync(string name, string category, string location)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Give the asset a name.");
        }
        return await assets.AddAsync(new Asset { Name = name.Trim(), Category = category.Trim(), Location = location.Trim() });
    }

    public async Task AddScheduleAsync(int assetId, string task, Frequency frequency, DateTime firstDue)
    {
        if (string.IsNullOrWhiteSpace(task) || frequency == Frequency.Once)
        {
            throw new ArgumentException("Give a task and a repeating frequency.");
        }
        var asset = await assets.GetAsync(assetId) ?? throw new ArgumentException("Unknown asset.");
        asset.Schedules.Add(new MaintenanceSchedule { Task = task.Trim(), Frequency = frequency, NextDue = firstDue.Date });
        await assets.UpdateAsync(asset);
    }

    /// <summary>Creates one work order per schedule that is due, then moves the schedule to its next future date.</summary>
    public async Task<int> GenerateDueWorkOrdersAsync()
    {
        var today = Now.Date;
        var created = 0;
        foreach (var asset in await assets.ListAsync())
        {
            var changed = false;
            foreach (var schedule in asset.Schedules.Where(s => s.NextDue.Date <= today))
            {
                await workOrders.AddAsync(new WorkOrder
                {
                    AssetId = asset.Id,
                    AssetName = asset.Name,
                    Task = schedule.Task,
                    DueDate = schedule.NextDue,
                    Status = WorkOrderStatus.Open
                });
                created++;

                while (schedule.NextDue.Date <= today && schedule.Frequency.Next(schedule.NextDue) is { } next)
                {
                    schedule.NextDue = next;
                }
                changed = true;
            }
            if (changed)
            {
                await assets.UpdateAsync(asset);
            }
        }
        return created;
    }

    public async Task CompleteWorkOrderAsync(int id, string notes, decimal cost, string actor)
    {
        var order = await workOrders.GetAsync(id) ?? throw new ArgumentException("Unknown work order.");
        if (order.Status == WorkOrderStatus.Done)
        {
            throw new InvalidOperationException("This work order is already done.");
        }
        order.Status = WorkOrderStatus.Done;
        order.CompletedAt = Now;
        order.CompletedBy = actor;
        order.Notes = notes.Trim();
        order.Cost = cost;
        await workOrders.UpdateAsync(order);
    }
}

public class ContractorService(IRepository<Contractor> contractors, IOptions<VillageOptions> options, TimeProvider time)
{
    private DateTime Today => time.GetLocalNow().Date;

    public async Task<List<Contractor>> ListAsync() => (await contractors.ListAsync()).OrderBy(c => c.Company).ToList();

    public async Task<Contractor> AddAsync(Contractor contractor)
    {
        if (string.IsNullOrWhiteSpace(contractor.Company))
        {
            throw new ArgumentException("Give the company name.");
        }
        return await contractors.AddAsync(contractor);
    }

    /// <summary>Adds a document or replaces the existing one of the same type.</summary>
    public async Task SaveDocumentAsync(int contractorId, ContractorDocType type, string reference, DateTime expiresOn)
    {
        var contractor = await contractors.GetAsync(contractorId) ?? throw new ArgumentException("Unknown contractor.");
        contractor.Documents.RemoveAll(d => d.Type == type);
        contractor.Documents.Add(new ContractorDocument { Type = type, Reference = reference.Trim(), ExpiresOn = expiresOn.Date });
        await contractors.UpdateAsync(contractor);
    }

    /// <summary>Reasons the contractor may not be appointed. Empty means compliant.</summary>
    public static List<string> Problems(Contractor contractor, DateTime today)
    {
        var problems = new List<string>();
        foreach (var type in Enum.GetValues<ContractorDocType>())
        {
            var doc = contractor.Documents.Where(d => d.Type == type).OrderByDescending(d => d.ExpiresOn).FirstOrDefault();
            if (doc is null)
            {
                problems.Add($"{Label(type)} is missing");
            }
            else if (doc.ExpiresOn.Date < today.Date)
            {
                problems.Add($"{Label(type)} expired on {Format.Date(doc.ExpiresOn)}");
            }
        }
        return problems;
    }

    public List<string> Problems(Contractor contractor) => Problems(contractor, Today);

    public bool ExpiresSoon(ContractorDocument doc) =>
        doc.ExpiresOn.Date >= Today && doc.ExpiresOn.Date <= Today.AddDays(options.Value.ContractorExpiryWarningDays);

    public static string Label(ContractorDocType type) => type switch
    {
        ContractorDocType.CoidaLetter => "COIDA letter of good standing",
        ContractorDocType.BbbeeCertificate => "B-BBEE certificate",
        ContractorDocType.PublicLiability => "Public liability insurance",
        ContractorDocType.OhsAgreement => "OHS s37(2) agreement",
        _ => "Safety file"
    };
}

public class NoticeService(IRepository<Notice> notices, TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Notice>> ActiveAsync() =>
        (await notices.ListAsync()).Where(n => n.ExpiresOn is null || n.ExpiresOn.Value.Date >= Now.Date)
            .OrderByDescending(n => n.PostedAt).ToList();

    public async Task PostAsync(string title, string body, DateTime? expiresOn, string actor)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Give the notice a title.");
        }
        await notices.AddAsync(new Notice { Title = title.Trim(), Body = body.Trim(), ExpiresOn = expiresOn, PostedAt = Now, PostedBy = actor });
    }

    public async Task ExpireAsync(int id)
    {
        var notice = await notices.GetAsync(id) ?? throw new ArgumentException("Unknown notice.");
        notice.ExpiresOn = Now.Date.AddDays(-1);
        await notices.UpdateAsync(notice);
    }
}
