using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class JobCardServiceTests
{
    [Fact]
    public async Task Job_moves_through_the_full_lifecycle_and_closes_when_fixed()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var jobs = app.Get<JobCardService>();

        var job = await jobs.LogAsync(resident.Id, "Plumbing", "Leaking tap", JobPriority.Normal);
        await jobs.MoveAsync(job.Id, JobStatus.Acknowledged, "Lindiwe");
        await jobs.ScheduleAsync(job.Id, TestApp.DefaultNow.AddDays(1), "08:00 - 12:00", "Lindiwe");
        await jobs.MoveAsync(job.Id, JobStatus.InProgress, "Johan");
        await jobs.MoveAsync(job.Id, JobStatus.Done, "Johan");
        var closed = await jobs.ConfirmFixedAsync(job.Id, true, resident.FullName, resident.Id);

        Assert.Equal(JobStatus.Closed, closed.Status);
        Assert.Equal("Lindiwe", closed.AcknowledgedBy);
        Assert.Equal(6, closed.History.Count);
    }

    [Fact]
    public async Task Not_fixed_reopens_the_job_and_restarts_the_service_time()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var jobs = app.Get<JobCardService>();
        var job = await DoneJobAsync(jobs, resident.Id);
        app.Clock.Now = TestApp.DefaultNow.AddDays(3);

        var reopened = await jobs.ConfirmFixedAsync(job.Id, false, resident.FullName, resident.Id);

        Assert.Equal(JobStatus.Acknowledged, reopened.Status);
        Assert.Equal(1, reopened.ReopenCount);
        Assert.Null(reopened.ScheduledFor);
        Assert.True(reopened.SlaDueAt > app.Clock.Now);
    }

    [Fact]
    public async Task Skipping_a_step_is_refused()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var jobs = app.Get<JobCardService>();
        var job = await jobs.LogAsync(resident.Id, "Plumbing", "", JobPriority.Normal);

        await Assert.ThrowsAsync<InvalidOperationException>(() => jobs.MoveAsync(job.Id, JobStatus.Done, "Johan"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => jobs.ScheduleAsync(job.Id, DateTime.Today, "x", "Johan"));
    }

    [Fact]
    public async Task Resident_cannot_confirm_someone_elses_job()
    {
        var app = new TestApp();
        var owner = await app.AddResidentAsync("Owner");
        var other = await app.AddResidentAsync("Other");
        var jobs = app.Get<JobCardService>();
        var job = await DoneJobAsync(jobs, owner.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => jobs.ConfirmFixedAsync(job.Id, true, other.FullName, other.Id));
    }

    [Fact]
    public async Task Urgent_job_breaches_after_configured_hours()
    {
        var app = new TestApp(o => o.Sla.UrgentHours = 2);
        var resident = await app.AddResidentAsync();
        var jobs = app.Get<JobCardService>();
        var job = await jobs.LogAsync(resident.Id, "Electrical", "No power", JobPriority.Urgent);

        app.Clock.Now = TestApp.DefaultNow.AddHours(1);
        Assert.False(jobs.IsSlaBreached(job));

        app.Clock.Now = TestApp.DefaultNow.AddHours(3);
        Assert.True(jobs.IsSlaBreached(job));
    }

    [Fact]
    public void Working_days_skip_the_weekend()
    {
        var friday = new DateTime(2026, 11, 6, 10, 0, 0);

        Assert.Equal(new DateTime(2026, 11, 10, 10, 0, 0), JobCardService.AddWorkingDays(friday, 2));
    }

    [Fact]
    public async Task Approved_charge_is_queued_for_Pastel_once()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var jobs = app.Get<JobCardService>();
        var job = await jobs.LogAsync(resident.Id, "Plumbing", "Inside unit", JobPriority.Normal);

        await jobs.MarkBillableAsync(job.Id, 350, "Johan");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => jobs.ApproveChargeAsync(job.Id, UserRole.Maintenance, "Johan"));
        await jobs.ApproveChargeAsync(job.Id, UserRole.FacilitiesManager, "Pieter");

        var outbox = await app.Repo<OutboxMessage>().ListAsync(m => m.Target == OutboxTarget.Pastel);
        var charge = Assert.Single(outbox);
        Assert.Equal($"JOB-{job.Id}-CHARGE", charge.Reference);
        Assert.Contains(resident.AccountNumber, charge.Payload);
    }

    [Fact]
    public async Task Stock_issued_to_a_job_is_costed_and_deducted()
    {
        var app = new TestApp(o => o.LabourRatePerHour = 200);
        var resident = await app.AddResidentAsync();
        var item = await app.Get<StockService>().AddItemAsync(new StockItem { Name = "Washer", OnHand = 10, Min = 2, Max = 20, UnitCost = 5 });
        var jobs = app.Get<JobCardService>();
        var job = await jobs.LogAsync(resident.Id, "Plumbing", "Tap", JobPriority.Normal);

        await jobs.IssueStockAsync(job.Id, item.Id, 2, "Johan");
        await jobs.AddCostsAsync(job.Id, 1.5m, 100, "Johan");

        var saved = (await jobs.GetAsync(job.Id))!;
        Assert.Equal(2 * 5 + 1.5m * 200 + 100, jobs.Cost(saved));
        Assert.Equal(8, (await app.Repo<StockItem>().GetAsync(item.Id))!.OnHand);
    }

    private static async Task<JobCard> DoneJobAsync(JobCardService jobs, int residentId)
    {
        var job = await jobs.LogAsync(residentId, "Plumbing", "Tap", JobPriority.Normal);
        await jobs.MoveAsync(job.Id, JobStatus.Acknowledged, "Lindiwe");
        await jobs.ScheduleAsync(job.Id, TestApp.DefaultNow.AddDays(1), "08:00 - 12:00", "Lindiwe");
        await jobs.MoveAsync(job.Id, JobStatus.InProgress, "Johan");
        return await jobs.MoveAsync(job.Id, JobStatus.Done, "Johan");
    }
}
