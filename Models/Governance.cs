namespace VillageCrm.Models;

public enum PolicyAudience { Staff, Residents, Both }

public enum PolicyStatus { Draft, Review, Approved, Superseded }

public class Policy : IEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Owner { get; set; } = "";
    public PolicyAudience Audience { get; set; }
    public List<PolicyVersion> Versions { get; set; } = new();
    public List<PolicyAcknowledgement> Acknowledgements { get; set; } = new();

    public PolicyVersion? Current => Versions.FirstOrDefault(v => v.Status == PolicyStatus.Approved);
    public PolicyVersion? Pending => Versions.FirstOrDefault(v => v.Status is PolicyStatus.Draft or PolicyStatus.Review);
}

public class PolicyVersion
{
    public int Number { get; set; }
    public PolicyStatus Status { get; set; }
    public string Body { get; set; } = "";
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ReviewDate { get; set; }
    public string ApprovedBy { get; set; } = "";
}

public class PolicyAcknowledgement
{
    public int VersionNumber { get; set; }
    public string Person { get; set; } = "";
    public DateTime At { get; set; }
}

public class ComplianceItem : IEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Law { get; set; } = "";
    public string Requirement { get; set; } = "";
    public Frequency Frequency { get; set; }
    public DateTime DueDate { get; set; }
    public string Owner { get; set; } = "";
    public string EvidenceRequired { get; set; } = "";
    public bool VerifyWithLegalAdvisor { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string Evidence { get; set; } = "";
}

public enum OutboxTarget { Pastel, Pilot }

public enum OutboxStatus { Pending, Sent, Failed }

/// <summary>A message waiting to go to Pastel or Pilot. Reference is unique per target (idempotent).</summary>
public class OutboxMessage : IEntity
{
    public int Id { get; set; }
    public OutboxTarget Target { get; set; }
    public string Type { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Payload { get; set; } = "";
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
