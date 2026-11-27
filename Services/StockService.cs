using VillageCrm.Data;
using VillageCrm.Integrations;
using VillageCrm.Models;

namespace VillageCrm.Services;

public class StockService(IRepository<StockItem> items, IPilotGateway pilot, TimeProvider time)
{
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<List<StockItem>> ListAsync(StockStore? store = null) =>
        (await items.ListAsync(i => store == null || i.Store == store)).OrderBy(i => i.Name).ToList();

    /// <summary>Takes bar and shop sales from Pilot off stock. Each sale reference is applied once.</summary>
    public async Task<int> ImportPilotSalesAsync(DateTime day, string actor)
    {
        var stock = await items.ListAsync(i => i.Store != StockStore.Maintenance);
        var applied = 0;
        foreach (var sale in await pilot.GetSalesAsync(day))
        {
            var item = stock.FirstOrDefault(i => i.Code == sale.ItemCode);
            if (item is null || item.Movements.Any(m => m.Reference == sale.Reference))
            {
                continue;
            }
            // The till already sold it, so stock may go below zero; a stock take corrects it.
            item.OnHand -= sale.Quantity;
            item.Movements.Insert(0, new StockMovement { When = Now, Quantity = -sale.Quantity, Reason = "Sold (Pilot)", Reference = sale.Reference, By = actor });
            await items.UpdateAsync(item);
            applied++;
        }
        return applied;
    }

    public async Task<StockItem> AddItemAsync(StockItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name) || item.Min < 0 || item.Max < item.Min)
        {
            throw new ArgumentException("Give a name, and a maximum that is not below the minimum.");
        }
        return await items.AddAsync(item);
    }

    public async Task<StockItem> IssueAsync(int itemId, decimal quantity, string reference, string actor)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be more than zero.");
        }
        var item = await RequireAsync(itemId);
        if (item.OnHand < quantity)
        {
            throw new InvalidOperationException($"Only {item.OnHand} {item.Unit} of {item.Name} in stock.");
        }

        item.OnHand -= quantity;
        item.Movements.Insert(0, new StockMovement { When = Now, Quantity = -quantity, Reason = "Issued", Reference = reference, By = actor });
        await items.UpdateAsync(item);
        return item;
    }

    /// <summary>Receives stock and updates the weighted average unit cost.</summary>
    public async Task<StockItem> ReceiveAsync(int itemId, decimal quantity, decimal unitCost, string reference, string actor)
    {
        if (quantity <= 0 || unitCost < 0)
        {
            throw new ArgumentException("Quantity must be more than zero and cost cannot be negative.");
        }
        var item = await RequireAsync(itemId);
        var onHand = Math.Max(item.OnHand, 0);
        item.UnitCost = Math.Round((onHand * item.UnitCost + quantity * unitCost) / (onHand + quantity), 2);
        item.OnHand += quantity;
        item.Movements.Insert(0, new StockMovement { When = Now, Quantity = quantity, Reason = "Received", Reference = reference, By = actor });
        await items.UpdateAsync(item);
        return item;
    }

    public async Task<StockItem> StockTakeAsync(int itemId, decimal counted, string actor)
    {
        if (counted < 0)
        {
            throw new ArgumentException("The count cannot be negative.");
        }
        var item = await RequireAsync(itemId);
        var difference = counted - item.OnHand;
        item.OnHand = counted;
        item.Movements.Insert(0, new StockMovement { When = Now, Quantity = difference, Reason = "Stock take adjustment", Reference = "STOCKTAKE", By = actor });
        await items.UpdateAsync(item);
        return item;
    }

    public async Task<List<StockItem>> ReorderListAsync() => (await ListAsync()).Where(i => i.NeedsReorder).ToList();

    public static decimal SuggestedOrder(StockItem item) => Math.Max(0, item.Max - item.OnHand);

    private async Task<StockItem> RequireAsync(int id) =>
        await items.GetAsync(id) ?? throw new ArgumentException("Unknown stock item.");
}
