namespace VillageCrm.Models;

public interface IEntity
{
    int Id { get; set; }
}

// New roles go at the end: the saved demo role is stored by number.
public enum UserRole { Resident, Reception, Clinical, Maintenance, FacilitiesManager, Procurement, Finance, Manager, SystemAdmin, RestaurantSupervisor }

public enum CareLevel { Independent, Assisted, FrailCare }

public enum ResidentStatus { InVillage, Away, Hospital, Deceased, Vacated }

public enum ContractType { LifeRight, SectionalTitle, Rental }

public enum MovementType
{
    LeftVillage, ReturnedToVillage, HospitalAdmission, HospitalDischarge, Holiday,
    CareLevelChange, UnitTransfer, Deceased, Vacated
}

public class StaffMember : IEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
}

public class Resident : IEntity
{
    public int Id { get; set; }

    // Shared with Pastel and Pilot. The platform is master for resident identity.
    public string AccountNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public string IdNumber { get; set; } = "";
    public string Unit { get; set; } = "";
    public DateTime OccupationDate { get; set; }
    public ContractType ContractType { get; set; }
    public CareLevel CareLevel { get; set; }
    public ResidentStatus Status { get; set; }
    public string Language { get; set; } = "English";
    public string PreferredContact { get; set; } = "Phone";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string NextOfKin { get; set; } = "";
    public string EmergencyContact { get; set; } = "";
    public string PowerOfAttorney { get; set; } = "";
    public string Doctor { get; set; } = "";
    public string PreferredHospital { get; set; } = "";
    public string MedicalAid { get; set; } = "";

    // Shared with the kitchen: dietary flag only, never a diagnosis.
    public string DietaryFlag { get; set; } = "";

    // POPIA consent register.
    public bool DirectoryOptIn { get; set; }
    public bool PhotoConsent { get; set; }
    public bool DataSharingConsent { get; set; }
    public bool WelfareMonitoringConsent { get; set; }

    /// <summary>Salted hash of the 4-digit kiosk PIN; empty means no PIN set.</summary>
    public string KioskPinHash { get; set; } = "";

    public List<MovementEntry> Movements { get; set; } = new();

    public bool IsActive => Status is not (ResidentStatus.Deceased or ResidentStatus.Vacated);
}

public class MovementEntry
{
    public DateTime When { get; set; }
    public MovementType Type { get; set; }
    public string Note { get; set; } = "";
    public string RecordedBy { get; set; } = "";
}

/// <summary>Clinical only. Kept apart from <see cref="Resident"/> so it is never loaded by accident.</summary>
public class MedicalRecord : IEntity
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string Conditions { get; set; } = "";
    public string Medication { get; set; } = "";
    public string Allergies { get; set; } = "";
    public string AdvanceDirective { get; set; } = "";
    public string MobilityAids { get; set; } = "";
}

public enum ChangeRequestStatus { Pending, Verified, Rejected }

public class ContactChangeRequest : IEntity
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string NextOfKin { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public ChangeRequestStatus Status { get; set; }
    public string? ReviewedBy { get; set; }
}

public class Notice : IEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime PostedAt { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string PostedBy { get; set; } = "";
}

public class AuditEntry : IEntity
{
    public int Id { get; set; }
    public DateTime When { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public int EntityId { get; set; }
    public string Detail { get; set; } = "";
}

public enum Frequency { Once, Daily, Weekly, Monthly, Quarterly, BiAnnual, Annual }

public static class FrequencyExtensions
{
    /// <summary>Next date after <paramref name="from"/>, or null for one-off items.</summary>
    public static DateTime? Next(this Frequency frequency, DateTime from) => frequency switch
    {
        Frequency.Daily => from.AddDays(1),
        Frequency.Weekly => from.AddDays(7),
        Frequency.Monthly => from.AddMonths(1),
        Frequency.Quarterly => from.AddMonths(3),
        Frequency.BiAnnual => from.AddMonths(6),
        Frequency.Annual => from.AddYears(1),
        _ => null
    };
}
