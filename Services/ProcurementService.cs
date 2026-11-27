using Microsoft.Extensions.Options;
using VillageCrm.Data;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class ProcurementService(
    IRepository<Purchase> purchases,
    StockService stock,
    OutboxService outbox,
    IOptions<VillageOptions> options,
    TimeProvider time)
{
    private VillageOptions Options => options.Value;
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<Purchase>> ListAsync() => (await purchases.ListAsync()).OrderByDescending(p => p.Id).ToList();

    public async Task<Purchase> CreateRequisitionAsync(string supplier, string reason, List<PurchaseLine> lines, string actor)
    {
        if (string.IsNullOrWhiteSpace(supplier) || lines.Count == 0
            || lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0 || string.IsNullOrWhiteSpace(l.Description)))
        {
            throw new ArgumentException("Give a supplier and at least one line with a description, quantity and price.");
        }

        return await purchases.AddAsync(new Purchase
        {
            Supplier = supplier.Trim(),
            Reason = reason.Trim(),
            RequestedBy = actor,
            RequestedAt = Now,
            Status = PurchaseStatus.AwaitingApproval,
            Lines = lines
        });
    }

    public string ApprovalRule(decimal total)
    {
        var limits = Options.Approvals;
        if (total <= limits.FacilitiesManagerLimit) return "Facilities manager or village manager";
        if (total <= limits.ManagerLimit) return "Village manager";
        return "Village manager and Finance";
    }

    public bool CanApprove(UserRole role, decimal total)
    {
        var limits = Options.Approvals;
        if (total <= limits.FacilitiesManagerLimit) return role is UserRole.FacilitiesManager or UserRole.Manager;
        if (total <= limits.ManagerLimit) return role is UserRole.Manager;
        return role is UserRole.Manager or UserRole.Finance;
    }

    public bool IsFullyApproved(Purchase purchase)
    {
        var limits = Options.Approvals;
        var roles = purchase.Approvals.Select(a => a.Role).ToHashSet();
        if (purchase.Total <= limits.FacilitiesManagerLimit) return roles.Contains(UserRole.FacilitiesManager) || roles.Contains(UserRole.Manager);
        if (purchase.Total <= limits.ManagerLimit) return roles.Contains(UserRole.Manager);
        return roles.Contains(UserRole.Manager) && roles.Contains(UserRole.Finance);
    }

    public async Task<Purchase> ApproveAsync(int id, UserRole role, string actor)
    {
        var purchase = await RequireAsync(id, PurchaseStatus.AwaitingApproval);
        if (!CanApprove(role, purchase.Total))
        {
            throw new UnauthorizedAccessException($"{Format.Money(purchase.Total)} needs approval by: {ApprovalRule(purchase.Total)}.");
        }
        if (purchase.Approvals.Any(a => a.Role == role))
        {
            throw new InvalidOperationException("Your role has already approved this.");
        }

        purchase.Approvals.Add(new Approval { Role = role, By = actor, At = Now });
        if (IsFullyApproved(purchase))
        {
            purchase.Status = PurchaseStatus.Approved;
        }
        await purchases.UpdateAsync(purchase);
        return purchase;
    }

    public async Task RejectAsync(int id, UserRole role, string actor, string reason)
    {
        var purchase = await RequireAsync(id, PurchaseStatus.AwaitingApproval);
        if (!CanApprove(role, purchase.Total))
        {
            throw new UnauthorizedAccessException($"Only {ApprovalRule(purchase.Total)} may reject this.");
        }
        purchase.Status = PurchaseStatus.Rejected;
        purchase.Note = $"Rejected by {actor}: {reason}";
        await purchases.UpdateAsync(purchase);
    }

    public async Task<Purchase> PlaceOrderAsync(int id, string actor)
    {
        var purchase = await RequireAsync(id, PurchaseStatus.Approved);
        purchase.PoNumber = $"PO-{id:00000}";
        purchase.Status = PurchaseStatus.Ordered;
        purchase.Note = $"Order placed by {actor}";
        await purchases.UpdateAsync(purchase);
        return purchase;
    }

    /// <summary>Goods received note. Stock lines are booked into stores at the order price.</summary>
    public async Task<Purchase> ReceiveGoodsAsync(int id, IReadOnlyList<decimal> receivedQuantities, string actor)
    {
        var purchase = await RequireAsync(id, PurchaseStatus.Ordered);
        if (receivedQuantities.Count != purchase.Lines.Count || receivedQuantities.Any(q => q < 0))
        {
            throw new ArgumentException("Give a received quantity (zero or more) for every line.");
        }

        for (var i = 0; i < purchase.Lines.Count; i++)
        {
            var line = purchase.Lines[i];
            var quantity = receivedQuantities[i];
            line.ReceivedQuantity += quantity;
            if (line.StockItemId is { } stockItemId && quantity > 0)
            {
                await stock.ReceiveAsync(stockItemId, quantity, line.UnitPrice, purchase.PoNumber, actor);
            }
        }
        purchase.Status = PurchaseStatus.Received;
        purchase.Note = $"Goods received by {actor}";
        await purchases.UpdateAsync(purchase);
        return purchase;
    }

    /// <summary>3-way match: order quantity = received quantity, and invoice = received value within tolerance.</summary>
    public async Task<Purchase> MatchInvoiceAsync(int id, string invoiceNumber, decimal invoiceAmount, string actor)
    {
        var purchase = await purchases.GetAsync(id) ?? throw new ArgumentException("Unknown purchase.");
        if (purchase.Status is not (PurchaseStatus.Received or PurchaseStatus.Mismatch))
        {
            throw new InvalidOperationException("Goods must be received before the invoice is matched.");
        }

        purchase.InvoiceNumber = invoiceNumber.Trim();
        purchase.InvoiceAmount = invoiceAmount;

        var problems = new List<string>();
        if (purchase.Lines.Any(l => l.ReceivedQuantity != l.Quantity))
        {
            problems.Add("received quantities differ from the order");
        }
        var receivedValue = purchase.Lines.Sum(l => l.ReceivedQuantity * l.UnitPrice);
        var tolerance = receivedValue * Options.InvoiceMatchTolerancePercent / 100m;
        if (Math.Abs(invoiceAmount - receivedValue) > tolerance)
        {
            problems.Add($"invoice {Format.Money(invoiceAmount)} differs from goods received {Format.Money(receivedValue)}");
        }

        if (problems.Count == 0)
        {
            purchase.Status = PurchaseStatus.Matched;
            purchase.Note = $"Order, delivery and invoice agree. Matched by {actor}.";
            await outbox.EnqueueAsync(OutboxTarget.Pastel, "SupplierInvoice", $"PUR-{id}-INV", new
            {
                purchase.Supplier,
                purchase.PoNumber,
                purchase.InvoiceNumber,
                Amount = invoiceAmount
            });
        }
        else
        {
            purchase.Status = PurchaseStatus.Mismatch;
            purchase.Note = "Mismatch: " + string.Join("; ", problems) + ".";
        }
        await purchases.UpdateAsync(purchase);
        return purchase;
    }

    private async Task<Purchase> RequireAsync(int id, PurchaseStatus expected)
    {
        var purchase = await purchases.GetAsync(id) ?? throw new ArgumentException("Unknown purchase.");
        if (purchase.Status != expected)
        {
            throw new InvalidOperationException($"This purchase is {Format.Words(purchase.Status)}, not {Format.Words(expected)}.");
        }
        return purchase;
    }
}
