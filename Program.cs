using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VillageCrm.Components;
using VillageCrm.Data.Mock;
using VillageCrm.Integrations;
using VillageCrm.Services;

// Runs entirely in the browser (Blazor WebAssembly), so it can be hosted as static files.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Data layer: in-memory mock (per browser tab, resets on refresh). Swap this line for a real API later.
builder.Services.AddMockDataLayer();

// Pastel and Pilot: fake connections. Swap this line for the real gateways later.
builder.Services.AddMockIntegrations();

// Business rules, settings ("Village" section of wwwroot/appsettings.json) and the mock viewer.
builder.Services.AddVillageServices(builder.Configuration);

await builder.Build().RunAsync();
