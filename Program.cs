using System.Text;
using VillageCrm.Components;
using VillageCrm.Data.Mock;
using VillageCrm.Integrations;
using VillageCrm.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Data layer: in-memory mock (resets on restart). Swap this line for a real database later.
builder.Services.AddMockDataLayer();

// Pastel and Pilot: fake connections. Swap this line for the real gateways later.
builder.Services.AddMockIntegrations();

// Business rules, settings ("Village" section of appsettings.json) and the mock viewer.
builder.Services.AddVillageServices(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

// Aggregate figures only (no personal data). Needs real authorisation once sign-in exists.
app.MapGet("/export/dashboard.csv", async (DashboardService dashboard) =>
    Results.File(Encoding.UTF8.GetBytes(DashboardService.ToCsv(await dashboard.GetAsync())),
        "text/csv", $"village-dashboard-{DateTime.Today:yyyyMMdd}.csv"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
