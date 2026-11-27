using Microsoft.Extensions.Options;
using VillageCrm.Integrations;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class GovernanceTests
{
    [Fact]
    public async Task Approving_a_new_version_supersedes_the_old_one_and_books_the_review()
    {
        var app = new TestApp();
        var policies = app.Get<PolicyService>();
        var policy = await policies.CreateAsync("Pets", "Village manager", PolicyAudience.Residents, "v1");
        await policies.SubmitForReviewAsync(policy.Id);
        await policies.ApproveAsync(policy.Id, TestApp.DefaultNow, TestApp.DefaultNow.AddYears(1), UserRole.Manager, "Grace");

        await policies.NewDraftAsync(policy.Id);
        await policies.EditDraftAsync(policy.Id, "v2");
        await policies.SubmitForReviewAsync(policy.Id);
        await policies.ApproveAsync(policy.Id, TestApp.DefaultNow.AddDays(1), TestApp.DefaultNow.AddYears(2), UserRole.Manager, "Grace");

        var saved = (await app.Repo<Policy>().GetAsync(policy.Id))!;
        Assert.Equal("v2", saved.Current!.Body);
        Assert.Equal(PolicyStatus.Superseded, saved.Versions.Single(v => v.Number == 1).Status);

        var review = Assert.Single(await app.Repo<ComplianceItem>().ListAsync(c => c.Title == "Policy review: Pets"));
        Assert.Equal(TestApp.DefaultNow.AddYears(2).Date, review.DueDate);
    }

    [Fact]
    public async Task Only_drafts_can_be_edited_and_only_manager_approves()
    {
        var app = new TestApp();
        var policies = app.Get<PolicyService>();
        var policy = await policies.CreateAsync("Visitors", "Village manager", PolicyAudience.Both, "v1");
        await policies.SubmitForReviewAsync(policy.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => policies.EditDraftAsync(policy.Id, "sneaky"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            policies.ApproveAsync(policy.Id, TestApp.DefaultNow, TestApp.DefaultNow.AddYears(1), UserRole.Reception, "Lindiwe"));
    }

    [Fact]
    public async Task Acknowledgement_is_counted_once_per_person()
    {
        var app = new TestApp();
        var policies = app.Get<PolicyService>();
        await app.AddResidentAsync("A");
        await app.AddResidentAsync("B");
        var policy = await policies.CreateAsync("Pets", "Village manager", PolicyAudience.Residents, "v1");
        await policies.SubmitForReviewAsync(policy.Id);
        await policies.ApproveAsync(policy.Id, TestApp.DefaultNow, TestApp.DefaultNow.AddYears(1), UserRole.Manager, "Grace");

        await policies.AcknowledgeAsync(policy.Id, "A");
        await policies.AcknowledgeAsync(policy.Id, "A");

        var saved = (await app.Repo<Policy>().GetAsync(policy.Id))!;
        Assert.Single(saved.Acknowledgements);
        Assert.Equal(0.5, PolicyService.AcknowledgementRate(saved, await policies.TargetCountAsync(saved)));
    }

    [Theory]
    [InlineData(-1, ComplianceState.Overdue, null)]
    [InlineData(0, ComplianceState.DueSoon, 1)]
    [InlineData(5, ComplianceState.DueSoon, 7)]
    [InlineData(14, ComplianceState.DueSoon, 14)]
    [InlineData(20, ComplianceState.DueSoon, 30)]
    [InlineData(45, ComplianceState.OnTrack, null)]
    public void Compliance_state_and_reminder_follow_the_due_date(int daysAhead, ComplianceState state, int? reminder)
    {
        var app = new TestApp();
        var compliance = app.Get<ComplianceService>();
        var item = new ComplianceItem { DueDate = TestApp.DefaultNow.AddDays(daysAhead) };

        Assert.Equal(state, compliance.StateOf(item));
        Assert.Equal(reminder, compliance.ReminderDays(item));
        Assert.Equal(state == ComplianceState.Overdue, compliance.IsEscalated(item));
    }

    [Fact]
    public async Task Closing_needs_evidence_and_creates_the_next_occurrence()
    {
        var app = new TestApp();
        var compliance = app.Get<ComplianceService>();
        await compliance.AddAsync(new ComplianceItem { Title = "EMP201", Owner = "Finance", Frequency = Frequency.Monthly, DueDate = new DateTime(2026, 11, 7) });
        var item = (await compliance.ListAsync(false)).Single();

        await Assert.ThrowsAsync<ArgumentException>(() => compliance.CloseAsync(item.Id, " ", "Rina"));
        await compliance.AcknowledgeAsync(item.Id, "Rina");
        await compliance.CloseAsync(item.Id, "SARS receipt 123", "Rina");

        var open = Assert.Single(await compliance.ListAsync(false));
        Assert.Equal(new DateTime(2026, 12, 7), open.DueDate);
        Assert.Equal(2, (await compliance.ListAsync(true)).Count);
    }

    [Fact]
    public async Task Outbox_is_idempotent_and_retries_when_Pastel_is_locked()
    {
        var app = new TestApp();
        var outbox = app.Get<OutboxService>();
        Assert.True(await outbox.EnqueueAsync(OutboxTarget.Pastel, "Test", "REF-1", new { A = 1 }));
        Assert.False(await outbox.EnqueueAsync(OutboxTarget.Pastel, "Test", "REF-1", new { A = 1 }));

        app.Get<IOptionsMonitor<VillageOptions>>().CurrentValue.SimulatePastelLocked = true;
        Assert.Equal((0, 1), await outbox.ProcessAsync());
        Assert.Equal(OutboxStatus.Failed, (await outbox.ListAsync()).Single().Status);

        app.Get<IOptionsMonitor<VillageOptions>>().CurrentValue.SimulatePastelLocked = false;
        Assert.Equal((1, 0), await outbox.ProcessAsync());
        var sent = (await outbox.ListAsync()).Single();
        Assert.Equal(OutboxStatus.Sent, sent.Status);
        Assert.Equal(2, sent.Attempts);
    }

    [Fact]
    public async Task Outbox_reports_queued_failed_and_sent_so_the_demo_can_show_the_flow()
    {
        var app = new TestApp();
        var events = new List<(string, IntegrationEvent)>();
        app.Get<IntegrationActivity>().Changed += (m, kind) => events.Add((m.Reference, kind));
        var outbox = app.Get<OutboxService>();
        var options = app.Get<IOptionsMonitor<VillageOptions>>().CurrentValue;

        await outbox.EnqueueAsync(OutboxTarget.Pastel, "Test", "REF-1", new { A = 1 });
        options.SimulatePastelLocked = true;
        await outbox.ProcessAsync();
        options.SimulatePastelLocked = false;
        await outbox.ProcessAsync();

        Assert.Equal([("REF-1", IntegrationEvent.Queued), ("REF-1", IntegrationEvent.Failed), ("REF-1", IntegrationEvent.Sent)], events);
    }

    [Fact]
    public async Task Simulated_Pastel_and_Pilot_show_only_what_they_received()
    {
        var app = new TestApp();
        var outbox = app.Get<OutboxService>();
        var external = app.Get<MockExternalSystems>();
        var resident = await app.AddResidentAsync(dietary: "Soft food", dataSharing: false);
        await outbox.EnqueueAsync(OutboxTarget.Pastel, "DebtorCharge", "JOB-9-CHARGE",
            new { resident.AccountNumber, Amount = 150m, Description = "Job 9: Plumbing" });

        Assert.Empty(await external.PastelPostingsAsync());
        Assert.Empty(await external.PilotCustomersAsync());

        await app.Get<ResidentService>().RecordMovementAsync(resident.Id, MovementType.Deceased, "", "Reception");
        await outbox.ProcessAsync();

        var posting = Assert.Single(await external.PastelPostingsAsync());
        Assert.Equal(("Debtors", resident.AccountNumber, 150m), (posting.Ledger, posting.Account, posting.Amount));
        var customer = Assert.Single(await external.PilotCustomersAsync()).Customer;
        Assert.False(customer.Active);
        Assert.Null(customer.DietaryFlag);
    }

    [Fact]
    public void Medical_access_is_clinical_only_and_residents_get_no_staff_links()
    {
        Assert.Equal([UserRole.Clinical], Enum.GetValues<UserRole>().Where(r => Access.Allows(r, Area.Medical)));
        Assert.DoesNotContain(Access.NavLinks(UserRole.Resident), l => l.Href is "residents" or "job-cards" or "audit");
    }

    [Fact]
    public void Every_role_starts_on_a_page_it_may_see()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var start = Access.StartPage(role);
            Assert.True(start == "./" || Access.NavLinks(role).Any(l => l.Href == start), $"{role} -> {start}");
        }
        Assert.Equal("./", Access.StartPage(UserRole.Resident));
    }

    [Fact]
    public async Task Dashboard_csv_neutralises_formulas()
    {
        var csv = DashboardService.ToCsv([new DashboardRow("Area", "=HYPERLINK(\"x\")", "1")]);

        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", csv);

        var app = new TestApp();
        await app.AddResidentAsync();
        Assert.Contains(await app.Get<DashboardService>().GetAsync(), r => r.Measure == "Active residents" && r.Value == "1");
    }
}
