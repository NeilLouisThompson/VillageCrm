using System.Collections.Concurrent;
using System.Text.Json;
using VillageCrm.Models;

namespace VillageCrm.Data.Mock;

/// <summary>
/// In-memory stand-in for the database. Rows are stored and handed out as copies,
/// so a change only sticks when UpdateAsync is called, exactly as with a real database.
/// </summary>
public class MockStore
{
    private readonly ConcurrentDictionary<Type, object> _tables = new();

    public MockTable<T> Table<T>() where T : class, IEntity =>
        (MockTable<T>)_tables.GetOrAdd(typeof(T), _ => new MockTable<T>());

    public static MockStore CreateSeeded()
    {
        var store = new MockStore();
        MockSeed.Populate(store);
        return store;
    }
}

public class MockTable<T> where T : class, IEntity
{
    private readonly List<T> _rows = new();
    private readonly object _gate = new();
    private int _nextId;

    public T? Get(int id)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            return row is null ? null : Copy(row);
        }
    }

    public List<T> List(Func<T, bool>? filter)
    {
        lock (_gate)
        {
            return _rows.Where(r => filter is null || filter(r)).Select(Copy).ToList();
        }
    }

    public T Add(T entity)
    {
        lock (_gate)
        {
            entity.Id = ++_nextId;
            _rows.Add(Copy(entity));
            return Copy(entity);
        }
    }

    public void Update(T entity)
    {
        lock (_gate)
        {
            var index = _rows.FindIndex(r => r.Id == entity.Id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"{typeof(T).Name} {entity.Id} was not found.");
            }
            _rows[index] = Copy(entity);
        }
    }

    public void Delete(int id)
    {
        lock (_gate)
        {
            _rows.RemoveAll(r => r.Id == id);
        }
    }

    private static T Copy(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
