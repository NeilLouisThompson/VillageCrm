using VillageCrm.Models;

namespace VillageCrm.Services;

public enum IntegrationEvent { Queued, Sending, Sent, Failed }

/// <summary>Tells the UI when a message for Pastel or Pilot is queued, sent or fails, so the demo can show the flow.</summary>
public class IntegrationActivity
{
    public event Action<OutboxMessage, IntegrationEvent>? Changed;

    public void Raise(OutboxMessage message, IntegrationEvent kind) => Changed?.Invoke(message, kind);

    public static string Label(string messageType) => messageType switch
    {
        "DebtorCharge" => "Resident charge",
        "SupplierInvoice" => "Supplier invoice",
        "ProjectCostJournal" => "Project cost journal",
        "CustomerAccount" => "Customer account",
        _ => messageType
    };
}
