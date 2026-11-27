using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class OperationsTests
{
    private static Contractor CompliantContractor(string name, DateTime expires) => new()
    {
        Company = name,
        Documents = Enum.GetValues<ContractorDocType>().Select(t => new ContractorDocument { Type = t, ExpiresOn = expires }).ToList()
    };

    [Fact]
    public void Contractor_with_expired_or_missing_document_has_problems()
    {
        var today = TestApp.DefaultNow;
        var contractor = CompliantContractor("A", today.AddYears(1));
        Assert.Empty(ContractorService.Problems(contractor, today));

        contractor.Documents[0].ExpiresOn = today.AddDays(-1);
        contractor.Documents.RemoveAt(1);

        var problems = ContractorService.Problems(contractor, today);
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("expired"));
        Assert.Contains(problems, p => p.Contains("missing"));
    }

    [Fact]
    public async Task Appointment_is_blocked_with_too_few_quotes_or_expired_documents()
    {
        var app = new TestApp(o => o.MinimumQuotes = 3);
        var contractors = app.Get<ContractorService>();
        var projects = app.Get<ProjectService>();
        var good = await contractors.AddAsync(CompliantContractor("Good", TestApp.DefaultNow.AddYears(1)));
        var expired = await contractors.AddAsync(CompliantContractor("Expired", TestApp.DefaultNow.AddDays(-5)));
        var third = await contractors.AddAsync(CompliantContractor("Third", TestApp.DefaultNow.AddYears(1)));
        var project = await projects.CreateAsync("Walkway", "", "CC-1", 100_000);

        await projects.AddQuoteAsync(project.Id, good.Id, 90_000);
        await projects.AddQuoteAsync(project.Id, expired.Id, 80_000);
        Assert.Contains(await projects.AppointAsync(project.Id, 1), p => p.Contains("3 quotes from different contractors"));

        await projects.AddQuoteAsync(project.Id, third.Id, 95_000);
        Assert.NotEmpty(await projects.AppointAsync(project.Id, 2));

        Assert.Empty(await projects.AppointAsync(project.Id, 1));
        var saved = (await projects.GetAsync(project.Id))!;
        Assert.Equal(ProjectStatus.Appointed, saved.Status);
        Assert.Equal(90_000, saved.ContractAmount);
    }

    [Fact]
    public async Task Repeat_quotes_from_the_same_contractor_do_not_count_towards_the_minimum()
    {
        var app = new TestApp(o => o.MinimumQuotes = 3);
        var contractors = app.Get<ContractorService>();
        var projects = app.Get<ProjectService>();
        var a = await contractors.AddAsync(CompliantContractor("A", TestApp.DefaultNow.AddYears(1)));
        var b = await contractors.AddAsync(CompliantContractor("B", TestApp.DefaultNow.AddYears(1)));
        var project = await projects.CreateAsync("Walkway", "", "CC-1", 100_000);

        await projects.AddQuoteAsync(project.Id, a.Id, 90_000);
        await projects.AddQuoteAsync(project.Id, b.Id, 92_000);
        await projects.AddQuoteAsync(project.Id, b.Id, 88_000);

        var problems = await projects.AppointAsync(project.Id, 1);
        Assert.Contains(problems, p => p.Contains("(there are 2)"));
        Assert.Equal(ProjectStatus.Planning, (await projects.GetAsync(project.Id))!.Status);
    }

    [Fact]
    public async Task Payment_certificate_holds_retention_and_posts_a_journal()
    {
        var app = new TestApp(o => { o.RetentionPercent = 10; o.MinimumQuotes = 1; });
        var projects = app.Get<ProjectService>();
        var contractor = await app.Get<ContractorService>().AddAsync(CompliantContractor("Good", TestApp.DefaultNow.AddYears(1)));
        var project = await projects.CreateAsync("Lights", "", "CC-2", 50_000);
        await projects.AddQuoteAsync(project.Id, contractor.Id, 40_000);

        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.AddPaymentCertificateAsync(project.Id, 10_000, "Cert 1"));
        Assert.Empty(await projects.AppointAsync(project.Id, 1));

        var retention = await projects.AddPaymentCertificateAsync(project.Id, 10_000, "Cert 1");

        var saved = (await projects.GetAsync(project.Id))!;
        Assert.Equal(1_000, retention);
        Assert.Equal(1_000, saved.RetentionHeld);
        Assert.Equal(10_000, saved.Actual);
        Assert.Single(await app.Repo<OutboxMessage>().ListAsync(m => m.Type == "ProjectCostJournal"));
    }

    [Fact]
    public async Task Approved_variations_raise_the_budget()
    {
        var app = new TestApp();
        var projects = app.Get<ProjectService>();
        var project = await projects.CreateAsync("Roof", "", "CC-3", 100_000);
        await projects.AddVariationAsync(project.Id, "Extra gutters", 5_000);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => projects.ApproveVariationAsync(project.Id, 0, UserRole.Finance));
        await projects.ApproveVariationAsync(project.Id, 0, UserRole.Manager);

        Assert.Equal(105_000, (await projects.GetAsync(project.Id))!.RevisedBudget);
    }

    [Fact]
    public async Task Maintenance_creates_due_work_orders_and_moves_the_schedule_on()
    {
        var app = new TestApp();
        var maintenance = app.Get<MaintenanceService>();
        var asset = await maintenance.AddAssetAsync("Lift", "Lift", "Block A");
        await maintenance.AddScheduleAsync(asset.Id, "Monthly service", Frequency.Monthly, TestApp.DefaultNow.AddDays(-40));
        await maintenance.AddScheduleAsync(asset.Id, "Annual inspection", Frequency.Annual, TestApp.DefaultNow.AddDays(10));

        Assert.Equal(1, await maintenance.GenerateDueWorkOrdersAsync());
        Assert.Equal(0, await maintenance.GenerateDueWorkOrdersAsync());

        var schedule = (await maintenance.AssetsAsync()).Single().Schedules.First(s => s.Task == "Monthly service");
        Assert.True(schedule.NextDue.Date > TestApp.DefaultNow.Date);
        Assert.Single(await maintenance.WorkOrdersAsync(openOnly: true));
    }
}
