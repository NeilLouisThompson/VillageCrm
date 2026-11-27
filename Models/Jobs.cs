namespace VillageCrm.Models;

public enum JobStatus { Logged, Acknowledged, Scheduled, InProgress, Done, Closed }

public enum JobPriority { Urgent, Normal, Low }

public enum ChargeStatus { NotBillable, AwaitingApproval, Approved }

public class JobCard : IEntity
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public string ResidentName { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public JobPriority Priority { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Logged;
    public DateTime LoggedAt { get; set; }
    public string? LoggedOnBehalfBy { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public string ScheduledWindow { get; set; } = "";
    public DateTime SlaDueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ReopenCount { get; set; }

    public decimal LabourHours { get; set; }
    public decimal ContractorCost { get; set; }
    public List<JobStockLine> StockLines { get; set; } = new();

    public ChargeStatus ChargeStatus { get; set; }
    public decimal ChargeAmount { get; set; }

    public List<string> History { get; set; } = new();

    public void Log(DateTime when, string text) => History.Add($"{when:dd MMM HH:mm}  {text}");
}

public class JobStockLine
{
    public int StockItemId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
