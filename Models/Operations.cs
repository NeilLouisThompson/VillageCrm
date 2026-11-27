namespace VillageCrm.Models;

public class Asset : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public List<MaintenanceSchedule> Schedules { get; set; } = new();
}

public class MaintenanceSchedule
{
    public string Task { get; set; } = "";
    public Frequency Frequency { get; set; }
    public DateTime NextDue { get; set; }
}

public enum WorkOrderStatus { Open, Done }

public class WorkOrder : IEntity
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AssetName { get; set; } = "";
    public string Task { get; set; } = "";
    public DateTime DueDate { get; set; }
    public WorkOrderStatus Status { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal Cost { get; set; }
    public string Notes { get; set; } = "";
}

public enum ContractorDocType { CoidaLetter, BbbeeCertificate, PublicLiability, OhsAgreement, SafetyFile }

public class Contractor : IEntity
{
    public int Id { get; set; }
    public string Company { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Trade { get; set; } = "";
    public List<ContractorDocument> Documents { get; set; } = new();
}

public class ContractorDocument
{
    public ContractorDocType Type { get; set; }
    public string Reference { get; set; } = "";
    public DateTime ExpiresOn { get; set; }
}

public enum ProjectStatus { Planning, Appointed, InProgress, Closed }

public class Project : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public string CostCentre { get; set; } = "";
    public decimal Budget { get; set; }
    public ProjectStatus Status { get; set; }
    public int? AppointedContractorId { get; set; }
    public string AppointedContractor { get; set; } = "";
    public decimal ContractAmount { get; set; }
    public decimal RetentionHeld { get; set; }
    public string CloseOutNotes { get; set; } = "";
    public List<Milestone> Milestones { get; set; } = new();
    public List<Quote> Quotes { get; set; } = new();
    public List<Variation> Variations { get; set; } = new();
    public List<ProjectCost> Costs { get; set; } = new();

    public decimal RevisedBudget => Budget + Variations.Where(v => v.Approved).Sum(v => v.Amount);
    public decimal Actual => Costs.Sum(c => c.Amount);
}

public class Milestone
{
    public string Name { get; set; } = "";
    public DateTime DueDate { get; set; }
    public bool Done { get; set; }
}

public class Quote
{
    public int Number { get; set; }
    public int ContractorId { get; set; }
    public string ContractorName { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime ReceivedOn { get; set; }
}

public class Variation
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Approved { get; set; }
}

public class ProjectCost
{
    public DateTime Date { get; set; }
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}
