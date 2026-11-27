using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VillageCrm.Services;

public static class VillageServiceExtensions
{
    /// <summary>Business services. These only depend on repository and gateway interfaces.</summary>
    public static IServiceCollection AddVillageServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VillageOptions>(configuration.GetSection(VillageOptions.Section));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IntegrationActivity>();

        services.AddScoped<ViewerContext>();
        services.AddScoped<AuditService>();
        services.AddScoped<OutboxService>();
        services.AddScoped<ResidentService>();
        services.AddScoped<JobCardService>();
        services.AddScoped<StockService>();
        services.AddScoped<ProcurementService>();
        services.AddScoped<MaintenanceService>();
        services.AddScoped<ContractorService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<NoticeService>();
        services.AddScoped<PolicyService>();
        services.AddScoped<ComplianceService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<RestaurantService>();
        return services;
    }
}
