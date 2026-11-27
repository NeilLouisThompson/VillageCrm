using VillageCrm.Integrations;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class AccountAndKioskTests
{
    [Fact]
    public async Task Statement_balance_is_the_sum_of_lines_and_includes_posted_job_charges()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var jobs = app.Get<JobCardService>();
        var job = await jobs.LogAsync(resident.Id, "Plumbing", "Inside unit", JobPriority.Normal);
        await jobs.MarkBillableAsync(job.Id, 350, "Johan");
        await jobs.ApproveChargeAsync(job.Id, UserRole.Manager, "Grace");
        var pastel = app.Get<IPastelGateway>();

        var before = await pastel.GetStatementAsync(resident.AccountNumber);
        Assert.DoesNotContain(before.Lines, l => l.Reference == $"JOB-{job.Id}-CHARGE");

        await app.Get<OutboxService>().ProcessAsync();
        var after = await pastel.GetStatementAsync(resident.AccountNumber);

        var charge = Assert.Single(after.Lines, l => l.Reference == $"JOB-{job.Id}-CHARGE");
        Assert.Equal(350, charge.Amount);
        Assert.Equal(after.Lines.Sum(l => l.Amount), after.Balance);
        Assert.Equal(before.Balance + 350, after.Balance);
    }

    [Fact]
    public async Task Kiosk_pin_is_hashed_and_verified()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var service = app.Get<ResidentService>();

        Assert.False(await service.VerifyKioskPinAsync(resident.Id, "1234"));

        await service.SetKioskPinAsync(resident.Id, "4821", "Lindiwe");
        var saved = (await service.GetAsync(resident.Id))!;

        Assert.DoesNotContain("4821", saved.KioskPinHash);
        Assert.True(await service.VerifyKioskPinAsync(resident.Id, "4821"));
        Assert.False(await service.VerifyKioskPinAsync(resident.Id, "4822"));
        Assert.Contains(await app.Repo<AuditEntry>().ListAsync(), a => a.Action == "Wrong kiosk PIN");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    public async Task Kiosk_pin_must_be_four_digits(string pin)
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => app.Get<ResidentService>().SetKioskPinAsync(resident.Id, pin, "Lindiwe"));
    }

    [Fact]
    public void Same_pin_hashes_differently_each_time()
    {
        Assert.NotEqual(PinHasher.Hash("1234"), PinHasher.Hash("1234"));
    }
}
