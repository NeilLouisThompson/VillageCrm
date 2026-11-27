using System.Linq.Expressions;
using VillageCrm.Models;

namespace VillageCrm.Data;

/// <summary>
/// The only way the app reads or writes stored data. Swap the implementation
/// (mock today, database later) in Program.cs; nothing else needs to change.
/// </summary>
public interface IRepository<T> where T : class, IEntity
{
    Task<T?> GetAsync(int id);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? filter = null);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}
