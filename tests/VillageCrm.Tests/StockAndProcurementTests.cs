using Microsoft.Extensions.Options;
using VillageCrm.Models;
using VillageCrm.Services;

namespace VillageCrm.Tests;

public class StockAndProcurementTests
{
    [Fact]
    public async Task Issue_reduces_stock_and_refuses_more_than_on_hand()
    {
        var app = new TestApp();
        var stock = app.Get<StockService>();
        var item = await stock.AddItemAsync(new StockItem { Name = "Globe", OnHand = 5, Min = 2, Max = 10, UnitCost = 30 });

        await stock.IssueAsync(item.Id, 3, "JOB-1", "Johan");

        Assert.True((await stock.ListAsync()).Single().NeedsReorder);
        Assert.Single(await stock.ReorderListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => stock.IssueAsync(item.Id, 3, "JOB-2", "Johan"));
    }

    [Fact]
    public async Task Receiving_uses_weighted_average_cost_and_stock_take_adjusts()
    {
        var app = new TestApp();
        var stock = app.Get<StockService>();
        var item = await stock.AddItemAsync(new StockItem { Name = "Globe", OnHand = 10, Min = 2, Max = 50, UnitCost = 10 });

        var received = await stock.ReceiveAsync(item.Id, 10, 20, "PO-1", "Zanele");
        Assert.Equal(15, received.UnitCost);
        Assert.Equal(20, received.OnHand);

        var counted = await stock.StockTakeAsync(item.Id, 18, "Zanele");
        Assert.Equal(18, counted.OnHand);
        Assert.Equal(-2, counted.Movements.First().Quantity);
        Assert.Equal(32, StockService.SuggestedOrder(counted));
    }

    [Theory]
    [InlineData(5_000, UserRole.FacilitiesManager, true)]
    [InlineData(5_000, UserRole.Manager, true)]
    [InlineData(5_000, UserRole.Procurement, false)]
    [InlineData(50_000, UserRole.FacilitiesManager, false)]
    [InlineData(50_000, UserRole.Manager, true)]
    [InlineData(150_000, UserRole.Finance, true)]
    [InlineData(150_000, UserRole.FacilitiesManager, false)]
    public void Approval_limits_follow_configuration(decimal total, UserRole role, bool allowed)
    {
        var app = new TestApp(o => { o.Approvals.FacilitiesManagerLimit = 10_000; o.Approvals.ManagerLimit = 100_000; });

        Assert.Equal(allowed, app.Get<ProcurementService>().CanApprove(role, total));
    }

    [Fact]
    public void Approval_limits_can_be_changed_in_settings()
    {
        var app = new TestApp(o => o.Approvals.FacilitiesManagerLimit = 60_000);

        Assert.True(app.Get<ProcurementService>().CanApprove(UserRole.FacilitiesManager, 50_000));
    }

    [Fact]
    public async Task Large_purchase_needs_manager_and_finance()
    {
        var app = new TestApp();
        var procurement = app.Get<ProcurementService>();
        var purchase = await procurement.CreateRequisitionAsync("CoolAir", "Aircon",
            [new PurchaseLine { Description = "Unit", Quantity = 6, UnitPrice = 20_000 }], "Pieter");

        var afterManager = await procurement.ApproveAsync(purchase.Id, UserRole.Manager, "Grace");
        Assert.Equal(PurchaseStatus.AwaitingApproval, afterManager.Status);

        var afterFinance = await procurement.ApproveAsync(purchase.Id, UserRole.Finance, "Rina");
        Assert.Equal(PurchaseStatus.Approved, afterFinance.Status);
    }

    [Fact]
    public async Task Full_purchase_flow_matches_invoice_receives_stock_and_posts_to_Pastel()
    {
        var app = new TestApp(o => o.InvoiceMatchTolerancePercent = 2);
        var stock = app.Get<StockService>();
        var item = await stock.AddItemAsync(new StockItem { Name = "Switch", OnHand = 0, Min = 5, Max = 20, UnitCost = 0 });
        var procurement = app.Get<ProcurementService>();
        var purchase = await procurement.CreateRequisitionAsync("Hardware", "Restock",
            [new PurchaseLine { StockItemId = item.Id, Description = "Switch", Quantity = 10, UnitPrice = 50 }], "Zanele");

        await procurement.ApproveAsync(purchase.Id, UserRole.FacilitiesManager, "Pieter");
        var ordered = await procurement.PlaceOrderAsync(purchase.Id, "Zanele");
        await procurement.ReceiveGoodsAsync(purchase.Id, [10], "Johan");

        var mismatch = await procurement.MatchInvoiceAsync(purchase.Id, "INV-1", 600, "Rina");
        Assert.Equal(PurchaseStatus.Mismatch, mismatch.Status);

        var matched = await procurement.MatchInvoiceAsync(purchase.Id, "INV-1", 505, "Rina");
        Assert.Equal(PurchaseStatus.Matched, matched.Status);
        Assert.Equal("PO-" + purchase.Id.ToString("00000"), ordered.PoNumber);
        Assert.Equal(10, (await app.Repo<StockItem>().GetAsync(item.Id))!.OnHand);
        Assert.Single(await app.Repo<OutboxMessage>().ListAsync(m => m.Type == "SupplierInvoice"));
    }

    [Fact]
    public async Task Short_delivery_does_not_match()
    {
        var app = new TestApp();
        var procurement = app.Get<ProcurementService>();
        var purchase = await procurement.CreateRequisitionAsync("Hardware", "Bits",
            [new PurchaseLine { Description = "Screws", Quantity = 10, UnitPrice = 1 }], "Zanele");
        await procurement.ApproveAsync(purchase.Id, UserRole.Manager, "Grace");
        await procurement.PlaceOrderAsync(purchase.Id, "Zanele");
        await procurement.ReceiveGoodsAsync(purchase.Id, [8], "Johan");

        var result = await procurement.MatchInvoiceAsync(purchase.Id, "INV-2", 8, "Rina");

        Assert.Equal(PurchaseStatus.Mismatch, result.Status);
        Assert.Contains("quantities", result.Note);
    }
}
