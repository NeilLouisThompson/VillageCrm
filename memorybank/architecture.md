# Architecture

## Project Structure

Standalone **Blazor WebAssembly** app on .NET 10 (`Microsoft.NET.Sdk.BlazorWebAssembly`). There is no server: the whole app, including services and mock data, runs in the browser and is hosted as static files on GitHub Pages.

| Folder | Contents |
|--------|----------|
| `Program.cs` | `WebAssemblyHostBuilder`; mounts `Routes` on `#app` and `HeadOutlet` on `head::after`; registers mock data, mock integrations and services |
| `wwwroot/` | `index.html` (host page + `downloadFile` JS helper), `app.css`, `appsettings.json` (the "Village" options) |
| `Components/` | `Routes.razor`, layouts, pages and shared components |
| `Services/` | Business services (scoped) and `Access` (role rules, nav links) |
| `Data/Mock/` | In-memory `MockStore` (singleton) and seed data |
| `Integrations/` | Mock Pastel/Pilot gateways (singleton) |
| `tests/VillageCrm.Tests/` | xUnit tests, building the real service graph via `TestApp` |
| `.github/workflows/deploy-pages.yml` | CI: test → publish → deploy to GitHub Pages |

## Key Modules

| Module | Responsibility |
|--------|---------------|
| `MockDataLayerExtensions` / `MockStore` | Repositories over in-memory tables. In WASM a singleton lives per browser tab |
| `VillageServiceExtensions` | Registers all business services and binds `VillageOptions` |
| `MainLayout` | Shell, navigation, demo role switcher (persisted in `localStorage` via `IJSRuntime`) |

## Data Flow

Page → service (scoped) → `IRepository<T>` → `MockStore`, all in the browser. Nothing is sent to a server; data resets when the page is reloaded.

## Key Design Decisions

- **WebAssembly, not Server** (TASK-003): needed for static hosting. Server-only APIs (`ProtectedLocalStorage`, minimal API endpoints, `HttpContext`) must not be used.
- **Base-relative links**: the site is served from `/VillageCrm/`, so internal `href`s must not start with `/` (use `residents`, `./`). The CI step rewrites `<base href>`.
- **Browser CPU cost**: .NET in WASM is much slower for heavy work such as PBKDF2. Avoid repeated expensive work at startup (seed hashes the demo PIN once).
