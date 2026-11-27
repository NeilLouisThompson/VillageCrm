using VillageCrm.Models;

namespace VillageCrm.Integrations;

/// <summary>Thrown when an external system cannot take the message right now (e.g. Pastel locked at month-end). The outbox retries.</summary>
public class IntegrationUnavailableException(string message) : Exception(message);

/// <summary>Sage Pastel (Partner or Evolution). Real implementation must use the official SDK or import formats only.</summary>
public interface IPastelGateway
{
    Task SendAsync(OutboxMessage message);
    Task<AccountStatement> GetStatementAsync(string accountNumber);
}

public record StatementLine(DateTime Date, string Description, string Reference, decimal Amount);

/// <summary>Read-only resident account from the finance system. Positive amounts are charges, negative are payments.</summary>
public record AccountStatement(string AccountNumber, decimal Balance, IReadOnlyList<StatementLine> Lines);

/// <summary>Pilot POS. Receives customer accounts and dietary flags only. Pilot posts to Pastel itself.</summary>
public interface IPilotGateway
{
    Task SendAsync(OutboxMessage message);

    /// <summary>Bar and shop items sold on the Pilot tills on a day. Pilot posts the money to Pastel itself.</summary>
    Task<IReadOnlyList<PilotSale>> GetSalesAsync(DateTime day);
}

public record PilotSale(string Reference, string ItemCode, decimal Quantity);

/// <summary>What Pilot is allowed to know about a resident. Never diagnoses.</summary>
public record PilotCustomer(string AccountNumber, string Name, string Unit, bool Active, string? DietaryFlag)
{
    public static PilotCustomer From(Resident r) =>
        new(r.AccountNumber, r.FullName, r.Unit, r.IsActive, r.DataSharingConsent ? r.DietaryFlag : null);
}
