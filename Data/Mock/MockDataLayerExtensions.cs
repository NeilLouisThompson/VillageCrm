namespace VillageCrm.Data.Mock;

public static class MockDataLayerExtensions
{
    /// <summary>Registers the in-memory data layer. Replace this call to use a real database.</summary>
    public static IServiceCollection AddMockDataLayer(this IServiceCollection services, bool seed = true)
    {
        services.AddSingleton(_ => seed ? MockStore.CreateSeeded() : new MockStore());
        services.AddSingleton(typeof(IRepository<>), typeof(MockRepository<>));
        return services;
    }
}
