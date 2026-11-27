# TASK-003: Publish to GitHub and host the demo on GitHub Pages

## Define
- **Goal**: Push the repo to https://github.com/NeilLouisThompson/VillageCrm and deploy the demo to GitHub Pages via a GitHub Actions pipeline, so it can be shared as a link.
- **Constraints**:
  - GitHub Pages serves static files only. The app is currently Blazor Server (needs a live .NET server + SignalR), so it must become Blazor WebAssembly (standalone).
  - Keep `net10.0`. Keep the mock data layer, services and pages unchanged in behaviour.
  - Site is served from a sub-path: `https://neillouisthompson.github.io/VillageCrm/`.
  - Smallest possible change; no unrelated refactoring.
- **Definition of Done**:
  - Code pushed to `main` on GitHub.
  - Workflow builds, runs tests, publishes and deploys to Pages on push to `main` (and manual dispatch).
  - Live site loads; navigation, deep links/refresh, role switcher (persisted), kiosk PIN and dashboard CSV export all work.
  - `dotnet build` and all tests pass locally and in CI.

## Discover

### Findings
- `Program.cs` uses `WebApplication` + `AddInteractiveServerComponents()`; `Components/App.razor` is the server host page (`blazor.web.js`, `@rendermode="InteractiveServer"`).
- All data is in-memory (`MockStore` singleton, mock Pastel/Pilot gateways). No file I/O, `HttpContext`, DB or external HTTP calls → WASM-compatible.
- **Server-only APIs in use**:
  - `MainLayout.razor` uses `ProtectedLocalStorage` (Server-only, relies on Data Protection) to remember the demo role.
  - `Program.cs` maps a minimal API endpoint `/export/dashboard.csv`, linked from `DashboardPage.razor`.
- **Root-absolute links** (`href="/residents"`, `"/"` etc.) in `Access.NavLinks`, `MainLayout`, `Routes`, `Home`, `CensusPage`, `Meals`, `ProjectDetail`, `ProjectsPage`, `ResidentDetail`, `ResidentsPage`, `StockPage`. Under the `/VillageCrm/` sub-path these would leave the app. Must become base-relative (`residents`, `./`).
  - `GovernanceTests.cs:121` asserts on `"/residents" or "/job-cards" or "/audit"` → needs updating to match.
- `appsettings.json` ("Village" options) must live in `wwwroot/` for WASM to load it.
- `Kiosk.razor` uses `System.Threading.Timer` — works in WASM.
- `Format` uses `InvariantCulture` → no globalization-data concerns.
- `PinHasher` uses PBKDF2 (100k iterations, SHA-256). Supported in .NET WASM, but `MockSeed` hashes 8 PINs at startup → may add a noticeable load delay in the browser. To be measured.
- Behaviour change (acceptable/beneficial for a demo): data becomes per-browser-tab and resets on refresh, instead of shared across all users until server restart.

### Affected Files
| File | Role | Notes |
|------|------|-------|
| `VillageCrm.csproj` | Project | SDK → `Microsoft.NET.Sdk.BlazorWebAssembly`, add WASM packages |
| `Program.cs` | Startup | `WebAssemblyHostBuilder`; drop CSV endpoint, HSTS etc. |
| `Components/App.razor` | Server host page | Delete — replaced by `wwwroot/index.html` |
| `wwwroot/index.html` | New | WASM host page + small CSV download JS helper |
| `appsettings.json` | Config | Move to `wwwroot/appsettings.json` |
| `Components/Layout/MainLayout.razor` | Layout | `ProtectedLocalStorage` → `localStorage` via `IJSRuntime`; relative links |
| `Components/Pages/DashboardPage.razor` | Page | CSV export → in-browser download |
| `Services/Access.cs` | Nav links | Relative hrefs |
| `Components/Routes.razor`, `Pages/*.razor` (9 files) | Pages | Relative hrefs |
| `tests/VillageCrm.Tests/GovernanceTests.cs` | Test | Update href assertion |
| `Properties/launchSettings.json` | Dev | Add `inspectUri` for WASM debugging |
| `.github/workflows/deploy-pages.yml` | New | Build/test/publish/deploy pipeline |
| `README.md`, memorybank docs | Docs | Run instructions, hosting notes |

### Root Cause / Area of Change
Hosting model: Blazor Server cannot run on static hosting. Convert to standalone Blazor WebAssembly and make all URLs base-relative.

### External Research
- Relevant docs/updates:
  - Host & deploy Blazor WASM on GitHub Pages: https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/github-pages — set `<base href>` to repo sub-path, add `.nojekyll`, copy `index.html` → `404.html` for deep links.
  - .NET 10 template uses `<OverrideHtmlAssetPlaceholders>` + `<link rel="preload" id="webassembly" />` + `blazor.webassembly#[.{fingerprint}].js`.
  - `ProtectedBrowserStorage` is not supported in Blazor WebAssembly (docs: "Protected browser storage relies on ASP.NET Core Data Protection and is only supported for Blazor Server").
- Package updates available: `Microsoft.AspNetCore.Components.WebAssembly` / `.DevServer` **10.0.15** (latest 10.x). Actions: checkout v7, setup-dotnet v6, configure-pages v6, upload-pages-artifact v5, deploy-pages v5.

### Discovery Summary
Feasible, low-risk conversion: ~2 new files, 1 deleted, 1 moved, ~15 small edits (mostly `href` strings). No business logic changes.

## Deliver
### Implementation Plan
- [ ] 0. (Done, pre-approved by request) `git init`, `.gitignore`, baseline commit of current Server version; push once repo exists.
- [ ] 1. `VillageCrm.csproj`: SDK → BlazorWebAssembly; `OverrideHtmlAssetPlaceholders`; add `Microsoft.AspNetCore.Components.WebAssembly` + `.DevServer` 10.0.15.
- [ ] 2. `Program.cs`: `WebAssemblyHostBuilder`, root `Routes` → `#app`, `HeadOutlet` → `head::after`; keep the three `Add…` registrations; remove CSV endpoint.
- [ ] 3. Add `wwwroot/index.html` (template-based, title "Village Care (Demo)", `app.css`, `downloadFile` JS helper); delete `Components/App.razor`.
- [ ] 4. Move `appsettings.json` → `wwwroot/appsettings.json`.
- [ ] 5. `MainLayout.razor`: replace `ProtectedLocalStorage` with `IJSRuntime` `localStorage` + `System.Text.Json` (same key, same `SavedViewer` record).
- [ ] 6. `DashboardPage.razor`: export button → `DashboardService.ToCsv(...)` → `downloadFile` JS (same filename format).
- [ ] 7. Make all internal links base-relative (`Access.NavLinks`, layouts, pages, `Routes`); update `GovernanceTests` assertion.
- [ ] 8. `launchSettings.json`: add `inspectUri`.
- [ ] 9. Measure startup; only if PIN seeding is slow, hash `"0000"` once in `MockSeed` and reuse for all seeded residents.
- [ ] 10. `.github/workflows/deploy-pages.yml`: on push to `main` + manual; setup .NET 10 → `dotnet test` → `dotnet publish` → set `<base href="/VillageCrm/">` → `404.html` + `.nojekyll` → upload & deploy.
- [ ] 11. Enable Pages (source = GitHub Actions) via `gh api`, push, watch the run, smoke-test the live URL (nav, deep link refresh, role switch, kiosk PIN 0000, CSV export).

### Approval
- **Status**: ✅ Approved
- **Approved by**: User (2026-11-27)
- **Notes**:

### Changes Made

## Demonstrate
### Test Results
- Build:
- Tests:
- Output:

### Human Verification
- **Status**: ⏳ Awaiting
- **Feedback**:

## Document
- **Summary**:
- **Documentation Updated**:
