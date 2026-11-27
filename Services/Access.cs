using VillageCrm.Models;

namespace VillageCrm.Services;

public enum Area
{
    Residents, Census, Medical, JobCards, Maintenance, Contractors, Projects, Stock, Procurement,
    Compliance, Dashboard, Integrations, Audit, ManageNotices, ManagePolicies, ApproveCharges,
    Restaurant, ManageRestaurant
}

/// <summary>Who may see what. One place to change role rules.</summary>
public static class Access
{
    private static readonly UserRole[] AllStaff =
        Enum.GetValues<UserRole>().Where(r => r != UserRole.Resident).ToArray();

    private static readonly Dictionary<Area, UserRole[]> Rules = new()
    {
        [Area.Residents] = [UserRole.Reception, UserRole.Clinical, UserRole.Manager],
        [Area.Census] = [UserRole.Reception, UserRole.Clinical, UserRole.Manager],
        [Area.Medical] = [UserRole.Clinical],
        [Area.JobCards] = [UserRole.Reception, UserRole.Maintenance, UserRole.FacilitiesManager, UserRole.Manager],
        [Area.Maintenance] = [UserRole.Maintenance, UserRole.FacilitiesManager, UserRole.Manager],
        [Area.Contractors] = [UserRole.FacilitiesManager, UserRole.Procurement, UserRole.Manager],
        [Area.Projects] = [UserRole.FacilitiesManager, UserRole.Finance, UserRole.Manager],
        [Area.Stock] = [UserRole.Maintenance, UserRole.FacilitiesManager, UserRole.Procurement, UserRole.Manager, UserRole.RestaurantSupervisor],
        [Area.Procurement] = [UserRole.FacilitiesManager, UserRole.Procurement, UserRole.Finance, UserRole.Manager],
        [Area.Compliance] = AllStaff,
        [Area.Dashboard] = [UserRole.FacilitiesManager, UserRole.Finance, UserRole.Manager],
        [Area.Integrations] = [UserRole.Finance, UserRole.Manager, UserRole.SystemAdmin],
        [Area.Audit] = [UserRole.Manager, UserRole.SystemAdmin],
        [Area.ManageNotices] = [UserRole.Reception, UserRole.Manager],
        [Area.ManagePolicies] = [UserRole.Manager],
        [Area.ApproveCharges] = [UserRole.FacilitiesManager, UserRole.Manager],
        [Area.Restaurant] = [UserRole.Reception, UserRole.RestaurantSupervisor, UserRole.Manager],
        [Area.ManageRestaurant] = [UserRole.RestaurantSupervisor, UserRole.Manager]
    };

    public static bool Allows(UserRole role, Area area) => Rules[area].Contains(role);

    /// <summary>Which stores a role works with on the stock screen.</summary>
    public static StockStore[] StoresFor(UserRole role) => role switch
    {
        UserRole.Maintenance or UserRole.FacilitiesManager => [StockStore.Maintenance],
        UserRole.RestaurantSupervisor => [StockStore.Bar, StockStore.Shop],
        _ => Enum.GetValues<StockStore>()
    };

    public static string WhoMay(Area area) => string.Join(", ", Rules[area].Select(Label));

    /// <summary>Where a user lands after switching to this role. Always a page the role may see.</summary>
    public static string StartPage(UserRole role) => role switch
    {
        UserRole.Reception or UserRole.Maintenance => "job-cards",
        UserRole.Clinical => "census",
        UserRole.FacilitiesManager => "assets",
        UserRole.Procurement => "procurement",
        UserRole.Finance or UserRole.SystemAdmin => "integrations",
        UserRole.Manager => "dashboard",
        UserRole.RestaurantSupervisor => "restaurant",
        _ => "./"
    };

    public static string Label(UserRole role) => role switch
    {
        UserRole.FacilitiesManager => "Facilities manager",
        UserRole.Manager => "Village manager",
        UserRole.SystemAdmin => "System admin",
        UserRole.RestaurantSupervisor => "Restaurant supervisor",
        _ => role.ToString()
    };

    public static IEnumerable<(string Href, string Text)> NavLinks(UserRole role)
    {
        if (role == UserRole.Resident)
        {
            yield return ("report", "Report a problem");
            yield return ("meals", "Meals");
            yield return ("my-requests", "My requests");
            yield return ("notices", "Notices");
            yield return ("policies", "Village rules");
            yield return ("directory", "Directory");
            yield return ("my-details", "My details");
            yield break;
        }

        if (Allows(role, Area.Dashboard)) yield return ("dashboard", "Dashboard");
        yield return ("report", "Log a job");
        if (Allows(role, Area.JobCards)) yield return ("job-cards", "Job cards");
        if (Allows(role, Area.Residents)) yield return ("residents", "Residents");
        if (Allows(role, Area.Census)) yield return ("census", "Census");
        if (Allows(role, Area.Restaurant)) yield return ("restaurant", "Restaurant");
        if (Allows(role, Area.Maintenance)) yield return ("assets", "Maintenance");
        if (Allows(role, Area.Projects)) yield return ("projects", "Projects");
        if (Allows(role, Area.Contractors)) yield return ("contractors", "Contractors");
        if (Allows(role, Area.Stock)) yield return ("stock", "Stock");
        if (Allows(role, Area.Procurement)) yield return ("procurement", "Procurement");
        yield return ("policies", "Policies");
        yield return ("compliance", "Compliance");
        yield return ("notices", "Notices");
        if (Allows(role, Area.Integrations)) yield return ("integrations", "Integrations");
        if (Allows(role, Area.Audit)) yield return ("audit", "Audit log");
        yield return ("kiosk", "Kiosk");
    }
}
