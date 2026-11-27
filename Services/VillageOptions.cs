namespace VillageCrm.Services;

/// <summary>Bound from the "Village" section of appsettings.json.</summary>
public class VillageOptions
{
    public const string Section = "Village";

    public string VillageName { get; set; } = "Village Care";
    public string ReceptionPhone { get; set; } = "012 000 0000";
    public string EmergencyPhone { get; set; } = "012 000 0911";

    // Arrays have no code defaults: the configuration binder appends to existing items.
    public string[] JobCategories { get; set; } = [];
    public string[] ScheduleWindows { get; set; } = [];
    public int[] ComplianceReminderDays { get; set; } = [];

    public SlaOptions Sla { get; set; } = new();
    public ApprovalOptions Approvals { get; set; } = new();

    public decimal InvoiceMatchTolerancePercent { get; set; } = 2;
    public int MinimumQuotes { get; set; } = 3;
    public decimal RetentionPercent { get; set; } = 10;
    public decimal LabourRatePerHour { get; set; } = 250;
    public int KioskTimeoutSeconds { get; set; } = 120;
    public int ContractorExpiryWarningDays { get; set; } = 30;
    public bool SimulatePastelLocked { get; set; }

    /// <summary>Reception is alerted when a consenting resident has no attended meal for this many days.</summary>
    public int WelfareNoMealDays { get; set; } = 2;
    public int MaxGuestsPerBooking { get; set; } = 4;

    public int DueSoonDays => ComplianceReminderDays.DefaultIfEmpty(30).Max();
}

public class SlaOptions
{
    public int UrgentHours { get; set; } = 2;
    public int NormalWorkingDays { get; set; } = 2;
    public int LowWorkingDays { get; set; } = 5;
}

public class ApprovalOptions
{
    /// <summary>Facilities manager (or village manager) may approve up to this amount.</summary>
    public decimal FacilitiesManagerLimit { get; set; } = 10_000;

    /// <summary>Village manager alone may approve up to this amount; above it Finance must also approve.</summary>
    public decimal ManagerLimit { get; set; } = 100_000;
}
