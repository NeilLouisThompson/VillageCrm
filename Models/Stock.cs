namespace VillageCrm.Models;

public enum StockStore { Maintenance, Bar, Shop }

public class StockItem : IEntity
{
    public int Id { get; set; }
    public StockStore Store { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Bin { get; set; } = "";
    public string Unit { get; set; } = "each";
    public decimal OnHand { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal UnitCost { get; set; }
    public List<StockMovement> Movements { get; set; } = new();

    public bool NeedsReorder => OnHand <= Min;
    public decimal Value => OnHand * UnitCost;
}

public class StockMovement
{
    public DateTime When { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = "";
    public string Reference { get; set; } = "";
    public string By { get; set; } = "";
}

public enum PurchaseStatus { AwaitingApproval, Approved, Rejected, Ordered, Received, Matched, Mismatch }

/// <summary>One purchase from requisition through PO, goods received and invoice match.</summary>
public class Purchase : IEntity
{
    public int Id { get; set; }
    public string Supplier { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public PurchaseStatus Status { get; set; }
    public List<PurchaseLine> Lines { get; set; } = new();
    public List<Approval> Approvals { get; set; } = new();
    public string PoNumber { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public decimal InvoiceAmount { get; set; }
    public string Note { get; set; } = "";

    public decimal Total => Lines.Sum(l => l.Quantity * l.UnitPrice);
}

public class PurchaseLine
{
    public int? StockItemId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ReceivedQuantity { get; set; }
}

public class Approval
{
    public UserRole Role { get; set; }
    public string By { get; set; } = "";
    public DateTime At { get; set; }
}
