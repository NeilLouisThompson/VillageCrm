# Static Hosting (Blazor WebAssembly on GitHub Pages)

## Overview
The demo runs entirely in the visitor's browser and is served as static files from https://neillouisthompson.github.io/VillageCrm/.

## Business Rules
- Each browser tab has its own in-memory data; it resets on refresh. Nothing is shared between visitors.
- The chosen demo role is remembered in `localStorage` (key `demo-viewer`, JSON `{ Role, ResidentId }`). Unreadable values fall back to the default role.
- Dashboard CSV export contains aggregate figures only and is generated in the browser.
- Every seeded resident's kiosk PIN is `0000`.

## Data Flow

```
Push to main → GitHub Actions (test → publish → set base href → 404.html + .nojekyll) → GitHub Pages
Browser → index.html → blazor.webassembly.js → Program.cs (WebAssemblyHostBuilder) → Routes → pages → services → MockStore
```

### Step-by-step:
1. A visitor opens any URL under `/VillageCrm/`. Unknown paths are served `404.html` (a copy of `index.html`), so deep links and refreshes still load the app.
2. `Program.cs` builds the services; `MockStore.CreateSeeded()` seeds the sample data.
3. `MainLayout` reads `demo-viewer` from `localStorage` and restores the role before rendering pages.
4. Dashboard export: `DashboardService.ToCsv` → JS `downloadFile(name, type, text)` in `index.html`.

## Validation Rules

| Field | Rule | Where Enforced | Error Message |
|-------|------|----------------|---------------|
| Saved role | Must deserialize to `SavedViewer` | `MainLayout.LoadSavedAsync` | None (falls back silently) |

## Key Files

| File | Role |
|------|------|
| `Program.cs` | WASM host setup |
| `wwwroot/index.html` | Host page, `<base href="/">` (rewritten in CI), `downloadFile` helper |
| `wwwroot/appsettings.json` | Village options (public; no secrets) |
| `Components/Layout/MainLayout.razor` | Role switcher persistence |
| `Components/Pages/DashboardPage.razor` | CSV export |
| `Services/Access.cs` | Nav links (base-relative) |
| `.github/workflows/deploy-pages.yml` | Build/test/deploy pipeline |

## Edge Cases & Gotchas
- **Never use root-absolute links** (`href="/x"`): they escape `/VillageCrm/`. Use `x` or `./`. `GovernanceTests` checks nav hrefs in this format.
- Deep-link requests show a 404 status in the browser console. This is expected (Pages serves `404.html`), and the app still loads.
- Server-only APIs (`ProtectedLocalStorage`, `MapGet` endpoints, `HttpContext`) do not exist in WASM.
- CPU-heavy code is slow in WASM: 8 × PBKDF2 (100k iterations) at seed time took about 13s, so the demo PIN is hashed once.
- Cold first load downloads the .NET runtime (several MB), which takes a few seconds.

## Related Components
- None documented yet.
