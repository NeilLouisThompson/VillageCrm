using VillageCrm.Models;

namespace VillageCrm.Tests;

public class MockRepositoryTests
{
    [Fact]
    public async Task Add_assigns_increasing_ids()
    {
        var repo = new TestApp().Repo<Notice>();

        var first = await repo.AddAsync(new Notice { Title = "One" });
        var second = await repo.AddAsync(new Notice { Title = "Two" });

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
    }

    [Fact]
    public async Task Changes_are_not_saved_until_UpdateAsync_is_called()
    {
        var repo = new TestApp().Repo<Notice>();
        var added = await repo.AddAsync(new Notice { Title = "Original" });

        var loaded = (await repo.GetAsync(added.Id))!;
        loaded.Title = "Changed but not saved";
        Assert.Equal("Original", (await repo.GetAsync(added.Id))!.Title);

        await repo.UpdateAsync(loaded);
        Assert.Equal("Changed but not saved", (await repo.GetAsync(added.Id))!.Title);
    }

    [Fact]
    public async Task List_filters_and_delete_removes()
    {
        var repo = new TestApp().Repo<Notice>();
        await repo.AddAsync(new Notice { Title = "Keep" });
        var drop = await repo.AddAsync(new Notice { Title = "Drop" });

        Assert.Single(await repo.ListAsync(n => n.Title == "Keep"));

        await repo.DeleteAsync(drop.Id);
        Assert.Null(await repo.GetAsync(drop.Id));
    }

    [Fact]
    public async Task Update_of_missing_row_throws()
    {
        var repo = new TestApp().Repo<Notice>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.UpdateAsync(new Notice { Id = 99 }));
    }

    [Fact]
    public void Seeded_store_has_data_for_every_module()
    {
        var store = VillageCrm.Data.Mock.MockStore.CreateSeeded();

        Assert.NotEmpty(store.Table<Resident>().List(null));
        Assert.NotEmpty(store.Table<MedicalRecord>().List(null));
        Assert.NotEmpty(store.Table<JobCard>().List(null));
        Assert.NotEmpty(store.Table<Asset>().List(null));
        Assert.NotEmpty(store.Table<Contractor>().List(null));
        Assert.NotEmpty(store.Table<Project>().List(null));
        Assert.NotEmpty(store.Table<StockItem>().List(null));
        Assert.NotEmpty(store.Table<Purchase>().List(null));
        Assert.NotEmpty(store.Table<Policy>().List(null));
        Assert.All(store.Table<ComplianceItem>().List(null), c => Assert.True(c.VerifyWithLegalAdvisor));
        Assert.All(store.Table<Resident>().List(null), r => Assert.False(string.IsNullOrEmpty(r.AccountNumber)));
    }
}
