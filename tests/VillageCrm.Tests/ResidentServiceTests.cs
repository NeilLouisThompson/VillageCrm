using System.Text.Json;
using VillageCrm.Integrations;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class ResidentServiceTests
{
    [Theory]
    [InlineData(MovementType.LeftVillage, ResidentStatus.Away)]
    [InlineData(MovementType.Holiday, ResidentStatus.Away)]
    [InlineData(MovementType.HospitalAdmission, ResidentStatus.Hospital)]
    [InlineData(MovementType.HospitalDischarge, ResidentStatus.InVillage)]
    [InlineData(MovementType.ReturnedToVillage, ResidentStatus.InVillage)]
    [InlineData(MovementType.Deceased, ResidentStatus.Deceased)]
    [InlineData(MovementType.Vacated, ResidentStatus.Vacated)]
    [InlineData(MovementType.UnitTransfer, ResidentStatus.Away)]
    public void Movement_sets_status(MovementType type, ResidentStatus expected)
    {
        var current = type == MovementType.UnitTransfer ? ResidentStatus.Away : ResidentStatus.InVillage;
        Assert.Equal(expected, ResidentService.NextStatus(current, type));
    }

    [Fact]
    public async Task Movement_is_logged_audited_and_changes_care_level()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var service = app.Get<ResidentService>();

        await service.RecordMovementAsync(resident.Id, MovementType.CareLevelChange, "Assessment", "Sister Anne", CareLevel.FrailCare);

        var saved = (await service.GetAsync(resident.Id))!;
        Assert.Equal(CareLevel.FrailCare, saved.CareLevel);
        var entry = Assert.Single(saved.Movements);
        Assert.Equal("Sister Anne", entry.RecordedBy);
        Assert.Contains(await app.Repo<AuditEntry>().ListAsync(), a => a.Action.StartsWith("Recorded movement"));
    }

    [Fact]
    public async Task Medical_record_is_refused_to_non_clinical_roles()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var service = app.Get<ResidentService>();

        foreach (var role in Enum.GetValues<UserRole>().Where(r => r != UserRole.Clinical))
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetMedicalAsync(resident.Id, role, "Someone"));
        }
    }

    [Fact]
    public async Task Viewing_a_medical_record_is_audited()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var service = app.Get<ResidentService>();

        await service.GetMedicalAsync(resident.Id, UserRole.Clinical, "Sister Anne");

        var audit = await app.Repo<AuditEntry>().ListAsync(a => a.Action == "Viewed medical record");
        Assert.Equal("Sister Anne", Assert.Single(audit).Actor);
    }

    [Fact]
    public async Task Pilot_only_receives_dietary_flag_with_consent_and_never_medical_data()
    {
        var withoutConsent = PilotCustomer.From(new Resident { AccountNumber = "RES1", FullName = "A", DietaryFlag = "Nut allergy", DataSharingConsent = false });
        var withConsent = PilotCustomer.From(new Resident { AccountNumber = "RES2", FullName = "B", DietaryFlag = "Nut allergy", DataSharingConsent = true });

        Assert.Null(withoutConsent.DietaryFlag);
        Assert.Equal("Nut allergy", withConsent.DietaryFlag);

        var app = new TestApp();
        var resident = await app.AddResidentAsync(dataSharing: true);
        await app.Get<ResidentService>().SaveMedicalAsync(new MedicalRecord { ResidentId = resident.Id, Conditions = "Secret diagnosis" }, UserRole.Clinical, "Sister Anne");
        await app.Get<ResidentService>().RecordMovementAsync(resident.Id, MovementType.Deceased, "", "Reception");

        var pilot = await app.Repo<OutboxMessage>().ListAsync(m => m.Target == OutboxTarget.Pilot);
        Assert.NotEmpty(pilot);
        Assert.DoesNotContain(pilot, m => m.Payload.Contains("Secret diagnosis"));
        var last = JsonSerializer.Deserialize<PilotCustomer>(pilot.Last().Payload)!;
        Assert.False(last.Active);
    }

    [Fact]
    public async Task Contact_change_applies_only_after_staff_verify()
    {
        var app = new TestApp();
        var resident = await app.AddResidentAsync();
        var service = app.Get<ResidentService>();

        await service.RequestContactChangeAsync(resident.Id, "083 111 2222", "new@example.com", "Son");
        Assert.NotEqual("083 111 2222", (await service.GetAsync(resident.Id))!.Phone);

        var request = Assert.Single(await service.PendingChangesAsync());
        await service.ReviewContactChangeAsync(request.Id, true, "Lindiwe");

        Assert.Equal("083 111 2222", (await service.GetAsync(resident.Id))!.Phone);
        Assert.Empty(await service.PendingChangesAsync());
    }

    [Fact]
    public async Task Census_excludes_residents_who_left()
    {
        var app = new TestApp();
        var service = app.Get<ResidentService>();
        var stays = await app.AddResidentAsync("Stays");
        var leaves = await app.AddResidentAsync("Leaves");
        await service.RecordMovementAsync(leaves.Id, MovementType.Vacated, "", "Reception");
        await service.RecordMovementAsync(stays.Id, MovementType.HospitalAdmission, "", "Reception");

        var census = await service.GetCensusAsync();

        Assert.Single(census.Residents);
        Assert.Equal(1, census.Hospital);
    }
}
