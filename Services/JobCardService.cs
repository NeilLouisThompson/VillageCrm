using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class JobCardService(
    IRepository<JobCard> jobs,
    IRepository<Resident> residents,
    StockService stock,
    OutboxService outbox,
    IOptions<VillageOptions> options,
    TimeProvider time)
{
    private VillageOptions Options => options.Value;
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<JobCard>> ListAsync(int? residentId = null) =>
        (await jobs.ListAsync())
            .Where(j => residentId is null || j.ResidentId == residentId)
            .OrderBy(j => j.Status == JobStatus.Closed)
            .ThenBy(j => j.Priority)
            .ThenByDescending(j => j.LoggedAt)
            .ToList();

    public Task<JobCard?> GetAsync(int id) => jobs.GetAsync(id);

    public async Task<JobCard> LogAsync(int residentId, string category, string description, JobPriority priority, string? onBehalfOf = null)
    {
        var resident = await residents.GetAsync(residentId) ?? throw new ArgumentException("Unknown resident.");
        var now = Now;
        var job = new JobCard
        {
            ResidentId = resident.Id,
            ResidentName = resident.FullName,
            Unit = resident.Unit,
            Category = category,
            Description = string.IsNullOrWhiteSpace(description) ? "No details given" : description.Trim(),
            Priority = priority,
            Status = JobStatus.Logged,
            LoggedAt = now,
            LoggedOnBehalfBy = onBehalfOf,
            SlaDueAt = SlaDue(now, priority)
        };
        job.Log(now, onBehalfOf is null ? "Logged by resident" : $"Logged by {onBehalfOf} for the resident");
        return await jobs.AddAsync(job);
    }

    public DateTime SlaDue(DateTime from, JobPriority priority) => priority switch
    {
        JobPriority.Urgent => from.AddHours(Options.Sla.UrgentHours),
        JobPriority.Normal => AddWorkingDays(from, Options.Sla.NormalWorkingDays),
        _ => AddWorkingDays(from, Options.Sla.LowWorkingDays)
    };

    public static DateTime AddWorkingDays(DateTime from, int days)
    {
        var date = from;
        while (days > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days--;
            }
        }
        return date;
    }

    public bool IsSlaBreached(JobCard job) => job.Status is not (JobStatus.Done or JobStatus.Closed) && Now > job.SlaDueAt;

    public static JobStatus? NextStep(JobStatus status) => status switch
    {
        JobStatus.Logged => JobStatus.Acknowledged,
        JobStatus.Acknowledged => JobStatus.Scheduled,
        JobStatus.Scheduled => JobStatus.InProgress,
        JobStatus.InProgress => JobStatus.Done,
        _ => null
    };

    public async Task<JobCard> MoveAsync(int id, JobStatus next, string actor)
    {
        var job = await RequireAsync(id);
        if (NextStep(job.Status) != next || next == JobStatus.Scheduled)
        {
            throw new InvalidOperationException($"A job that is {Format.Words(job.Status)} cannot move to {Format.Words(next)}.");
        }

        job.Status = next;
        if (next == JobStatus.Acknowledged)
        {
            job.AcknowledgedBy = actor;
        }
        if (next == JobStatus.Done)
        {
            job.CompletedAt = Now;
        }
        job.Log(Now, $"{Format.Words(next)} by {actor}");
        await jobs.UpdateAsync(job);
        return job;
    }

    public async Task<JobCard> ScheduleAsync(int id, DateTime date, string window, string actor)
    {
        var job = await RequireAsync(id);
        if (job.Status != JobStatus.Acknowledged)
        {
            throw new InvalidOperationException("Only acknowledged jobs can be scheduled.");
        }

        job.Status = JobStatus.Scheduled;
        job.ScheduledFor = date.Date;
        job.ScheduledWindow = window;
        job.Log(Now, $"Scheduled for {date:dd MMM} {window} by {actor}");
        await jobs.UpdateAsync(job);
        return job;
    }

    /// <summary>Resident answers "Fixed?". No reopens the job. Pass residentId when the resident answers for themselves.</summary>
    public async Task<JobCard> ConfirmFixedAsync(int id, bool isFixed, string answeredBy, int? residentId = null)
    {
        var job = await RequireAsync(id);
        if (residentId is not null && job.ResidentId != residentId)
        {
            throw new UnauthorizedAccessException("This is not your request.");
        }
        if (job.Status != JobStatus.Done)
        {
            throw new InvalidOperationException("Only finished jobs can be confirmed.");
        }

        if (isFixed)
        {
            job.Status = JobStatus.Closed;
            job.Log(Now, $"Confirmed fixed ({answeredBy})");
        }
        else
        {
            job.Status = JobStatus.Acknowledged;
            job.ReopenCount++;
            job.ScheduledFor = null;
            job.ScheduledWindow = "";
            job.CompletedAt = null;
            job.SlaDueAt = SlaDue(Now, job.Priority);
            job.Log(Now, $"Not fixed - reopened ({answeredBy})");
        }
        await jobs.UpdateAsync(job);
        return job;
    }

    public async Task AddCostsAsync(int id, decimal labourHours, decimal contractorCost, string actor)
    {
        if (labourHours < 0 || contractorCost < 0)
        {
            throw new ArgumentException("Costs cannot be negative.");
        }
        var job = await RequireAsync(id);
        job.LabourHours += labourHours;
        job.ContractorCost += contractorCost;
        job.Log(Now, $"Costs added by {actor}: {labourHours} h labour, {Format.Money(contractorCost)} contractor");
        await jobs.UpdateAsync(job);
    }

    public async Task IssueStockAsync(int id, int stockItemId, decimal quantity, string actor)
    {
        var job = await RequireAsync(id);
        var item = await stock.IssueAsync(stockItemId, quantity, $"JOB-{id}", actor);
        job.StockLines.Add(new JobStockLine
        {
            StockItemId = item.Id,
            Description = item.Name,
            Quantity = quantity,
            UnitCost = item.UnitCost
        });
        job.Log(Now, $"{quantity} x {item.Name} issued by {actor}");
        await jobs.UpdateAsync(job);
    }

    public decimal Cost(JobCard job) =>
        job.LabourHours * Options.LabourRatePerHour
        + job.StockLines.Sum(l => l.Quantity * l.UnitCost)
        + job.ContractorCost;

    public async Task MarkBillableAsync(int id, decimal amount, string actor)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("The charge must be more than zero.");
        }
        var job = await RequireAsync(id);
        if (job.ChargeStatus == ChargeStatus.Approved)
        {
            throw new InvalidOperationException("The charge has already been approved.");
        }
        job.ChargeStatus = ChargeStatus.AwaitingApproval;
        job.ChargeAmount = amount;
        job.Log(Now, $"Charge of {Format.Money(amount)} proposed by {actor}");
        await jobs.UpdateAsync(job);
    }

    public async Task ApproveChargeAsync(int id, UserRole role, string actor)
    {
        if (!Access.Allows(role, Area.ApproveCharges))
        {
            throw new UnauthorizedAccessException($"Charges can be approved by: {Access.WhoMay(Area.ApproveCharges)}.");
        }
        var job = await RequireAsync(id);
        if (job.ChargeStatus != ChargeStatus.AwaitingApproval)
        {
            throw new InvalidOperationException("There is no charge waiting for approval.");
        }
        var resident = await residents.GetAsync(job.ResidentId) ?? throw new ArgumentException("Unknown resident.");

        job.ChargeStatus = ChargeStatus.Approved;
        job.Log(Now, $"Charge approved by {actor}");
        await jobs.UpdateAsync(job);
        await outbox.EnqueueAsync(OutboxTarget.Pastel, "DebtorCharge", $"JOB-{id}-CHARGE", new
        {
            resident.AccountNumber,
            Amount = job.ChargeAmount,
            Description = $"Job {id}: {job.Category}"
        });
    }

    public static string ResidentMessage(JobCard job) => job.Status switch
    {
        JobStatus.Logged => "We have received your request.",
        JobStatus.Acknowledged => $"{job.AcknowledgedBy} is looking after your request.",
        JobStatus.Scheduled => $"Someone will come on {job.ScheduledFor:dddd d MMMM}, between {job.ScheduledWindow}.",
        JobStatus.InProgress => "Work on your request has started.",
        JobStatus.Done => "The work is finished. Is it fixed?",
        _ => "Fixed. Thank you for letting us know."
    };

    private async Task<JobCard> RequireAsync(int id) =>
        await jobs.GetAsync(id) ?? throw new ArgumentException("Unknown job card.");
}
