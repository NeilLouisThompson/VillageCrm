using VillageCrm.Data;
using VillageCrm.Integrations;
using VillageCrm.Models;

namespace VillageCrm.Services;

public record Census(int InVillage, int Away, int Hospital, int FrailCare, List<Resident> Residents);

public class ResidentService(
    IRepository<Resident> residents,
    IRepository<MedicalRecord> medical,
    IRepository<ContactChangeRequest> changes,
    AuditService audit,
    OutboxService outbox,
    TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Resident>> ListAsync(string? search = null, bool includeInactive = false) =>
        (await residents.ListAsync())
            .Where(r => includeInactive || r.IsActive)
            .Where(r => string.IsNullOrWhiteSpace(search)
                || r.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || r.Unit.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Unit)
            .ToList();

    public Task<Resident?> GetAsync(int id) => residents.GetAsync(id);

    public async Task<Resident> GetForStaffAsync(int id, string actor)
    {
        var resident = await RequireAsync(id);
        await audit.WriteAsync(actor, "Viewed resident record", nameof(Resident), id);
        return resident;
    }

    public async Task<Resident> AddAsync(Resident resident, string actor)
    {
        if (string.IsNullOrWhiteSpace(resident.FullName) || string.IsNullOrWhiteSpace(resident.Unit))
        {
            throw new ArgumentException("Name and unit are required.");
        }

        resident.Status = ResidentStatus.InVillage;
        var added = await residents.AddAsync(resident);
        if (string.IsNullOrWhiteSpace(added.AccountNumber))
        {
            added.AccountNumber = $"RES{added.Id:0000}";
            await residents.UpdateAsync(added);
        }
        await audit.WriteAsync(actor, "Added resident", nameof(Resident), added.Id, added.FullName);
        await SyncToPilotAsync(added);
        return added;
    }

    public async Task UpdateProfileAsync(Resident resident, string actor)
    {
        await residents.UpdateAsync(resident);
        await audit.WriteAsync(actor, "Updated resident profile", nameof(Resident), resident.Id);
        await SyncToPilotAsync(resident);
    }

    public async Task<MedicalRecord> GetMedicalAsync(int residentId, UserRole role, string actor)
    {
        EnsureClinical(role);
        var record = (await medical.ListAsync(m => m.ResidentId == residentId)).FirstOrDefault()
            ?? new MedicalRecord { ResidentId = residentId };
        await audit.WriteAsync(actor, "Viewed medical record", nameof(Resident), residentId);
        return record;
    }

    public async Task SaveMedicalAsync(MedicalRecord record, UserRole role, string actor)
    {
        EnsureClinical(role);
        if (record.Id == 0)
        {
            await medical.AddAsync(record);
        }
        else
        {
            await medical.UpdateAsync(record);
        }
        await audit.WriteAsync(actor, "Updated medical record", nameof(Resident), record.ResidentId);
    }

    public static ResidentStatus NextStatus(ResidentStatus current, MovementType type) => type switch
    {
        MovementType.LeftVillage or MovementType.Holiday => ResidentStatus.Away,
        MovementType.HospitalAdmission => ResidentStatus.Hospital,
        MovementType.ReturnedToVillage or MovementType.HospitalDischarge => ResidentStatus.InVillage,
        MovementType.Deceased => ResidentStatus.Deceased,
        MovementType.Vacated => ResidentStatus.Vacated,
        _ => current
    };

    public async Task RecordMovementAsync(int residentId, MovementType type, string note, string actor,
        CareLevel? newCareLevel = null, string? newUnit = null)
    {
        var resident = await RequireAsync(residentId);
        resident.Status = NextStatus(resident.Status, type);

        if (type == MovementType.CareLevelChange && newCareLevel is { } level)
        {
            note = $"{Format.Words(resident.CareLevel)} to {Format.Words(level)}. {note}".Trim();
            resident.CareLevel = level;
        }
        if (type == MovementType.UnitTransfer && !string.IsNullOrWhiteSpace(newUnit))
        {
            note = $"{resident.Unit} to {newUnit}. {note}".Trim();
            resident.Unit = newUnit;
        }

        resident.Movements.Insert(0, new MovementEntry { When = Now, Type = type, Note = note, RecordedBy = actor });
        await residents.UpdateAsync(resident);
        await audit.WriteAsync(actor, $"Recorded movement: {Format.Words(type)}", nameof(Resident), resident.Id, note);
        await SyncToPilotAsync(resident);
    }

    public async Task<Census> GetCensusAsync()
    {
        var active = await ListAsync();
        return new Census(
            active.Count(r => r.Status == ResidentStatus.InVillage),
            active.Count(r => r.Status == ResidentStatus.Away),
            active.Count(r => r.Status == ResidentStatus.Hospital),
            active.Count(r => r.CareLevel == CareLevel.FrailCare),
            active);
    }

    public async Task RequestContactChangeAsync(int residentId, string phone, string email, string nextOfKin)
    {
        var resident = await RequireAsync(residentId);
        var request = await changes.AddAsync(new ContactChangeRequest
        {
            ResidentId = residentId,
            ResidentName = resident.FullName,
            Phone = phone.Trim(),
            Email = email.Trim(),
            NextOfKin = nextOfKin.Trim(),
            RequestedAt = Now,
            Status = ChangeRequestStatus.Pending
        });
        await audit.WriteAsync(resident.FullName, "Requested contact details change", nameof(ContactChangeRequest), request.Id);
    }

    public async Task<List<ContactChangeRequest>> PendingChangesAsync(int? residentId = null) =>
        (await changes.ListAsync(c => c.Status == ChangeRequestStatus.Pending))
            .Where(c => residentId is null || c.ResidentId == residentId)
            .OrderBy(c => c.RequestedAt)
            .ToList();

    public async Task ReviewContactChangeAsync(int requestId, bool approve, string actor)
    {
        var request = await changes.GetAsync(requestId) ?? throw new ArgumentException("Unknown request.");
        if (request.Status != ChangeRequestStatus.Pending)
        {
            throw new InvalidOperationException("This request has already been reviewed.");
        }

        if (approve)
        {
            var resident = await RequireAsync(request.ResidentId);
            resident.Phone = request.Phone;
            resident.Email = request.Email;
            resident.NextOfKin = request.NextOfKin;
            await residents.UpdateAsync(resident);
        }

        request.Status = approve ? ChangeRequestStatus.Verified : ChangeRequestStatus.Rejected;
        request.ReviewedBy = actor;
        await changes.UpdateAsync(request);
        await audit.WriteAsync(actor, approve ? "Verified contact change" : "Rejected contact change",
            nameof(Resident), request.ResidentId);
    }

    public async Task UpdateConsentAsync(int residentId, bool directory, bool photo, bool dataSharing, bool welfare, string actor)
    {
        var resident = await RequireAsync(residentId);
        resident.DirectoryOptIn = directory;
        resident.PhotoConsent = photo;
        resident.DataSharingConsent = dataSharing;
        resident.WelfareMonitoringConsent = welfare;
        await residents.UpdateAsync(resident);
        await audit.WriteAsync(actor, "Updated consent", nameof(Resident), residentId,
            $"Directory={directory}, Photo={photo}, DataSharing={dataSharing}, Welfare={welfare}");
        await SyncToPilotAsync(resident);
    }

    public async Task<List<Resident>> DirectoryAsync() =>
        (await ListAsync()).Where(r => r.DirectoryOptIn).OrderBy(r => r.FullName).ToList();

    public async Task SetKioskPinAsync(int residentId, string pin, string actor)
    {
        if (!PinHasher.IsValidFormat(pin))
        {
            throw new ArgumentException("The PIN must be exactly 4 digits.");
        }
        var resident = await RequireAsync(residentId);
        resident.KioskPinHash = PinHasher.Hash(pin);
        await residents.UpdateAsync(resident);
        await audit.WriteAsync(actor, "Set kiosk PIN", nameof(Resident), residentId);
    }

    public async Task<bool> VerifyKioskPinAsync(int residentId, string pin)
    {
        var resident = await RequireAsync(residentId);
        var ok = resident.KioskPinHash != "" && PinHasher.IsValidFormat(pin) && PinHasher.Verify(pin, resident.KioskPinHash);
        if (!ok)
        {
            await audit.WriteAsync("Kiosk", "Wrong kiosk PIN", nameof(Resident), residentId);
        }
        return ok;
    }

    private static void EnsureClinical(UserRole role)
    {
        if (!Access.Allows(role, Area.Medical))
        {
            throw new UnauthorizedAccessException("Medical records are restricted to clinical staff.");
        }
    }

    private async Task<Resident> RequireAsync(int id) =>
        await residents.GetAsync(id) ?? throw new ArgumentException("Unknown resident.");

    // Each profile change is a new message; the reference only has to be unique.
    private Task SyncToPilotAsync(Resident r) =>
        outbox.EnqueueAsync(OutboxTarget.Pilot, "CustomerAccount", $"PILOT-{r.AccountNumber}-{Guid.NewGuid():N}", PilotCustomer.From(r));
}
