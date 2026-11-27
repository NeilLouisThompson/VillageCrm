using System.Linq.Expressions;
using VillageCrm.Models;

namespace VillageCrm.Data.Mock;

public class MockRepository<T>(MockStore store) : IRepository<T> where T : class, IEntity
{
    private MockTable<T> Table => store.Table<T>();

    public Task<T?> GetAsync(int id) => Task.FromResult(Table.Get(id));

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? filter = null) =>
        Task.FromResult(Table.List(filter?.Compile()));

    public Task<T> AddAsync(T entity) => Task.FromResult(Table.Add(entity));

    public Task UpdateAsync(T entity)
    {
        Table.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        Table.Delete(id);
        return Task.CompletedTask;
    }
}
