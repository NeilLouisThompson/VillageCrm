# Setup

## Prerequisites

- .NET 10 SDK (a newer SDK also works; the project targets `net10.0`)
- Optional: `wasm-tools` workload (`dotnet workload install wasm-tools`) for smaller, optimized publish output

## Environment Variables

None. Settings live in `wwwroot/appsettings.json` ("Village" section). That file is downloaded by the browser, so it must never contain secrets.

| Variable | Purpose | Required | Default |
|----------|---------|----------|---------|

## Getting Started

```bash
git clone https://github.com/NeilLouisThompson/VillageCrm.git
cd VillageCrm
dotnet run          # dev server, see Properties/launchSettings.json
```

## Running Tests

```bash
dotnet test VillageCrm.slnx
```

## Deployment

- Every push to `main` runs `.github/workflows/deploy-pages.yml`: test → `dotnet publish -c Release` → set `<base href="/VillageCrm/">` → copy `index.html` to `404.html` (deep links) → add `.nojekyll` → deploy to GitHub Pages.
- It can also be started manually (`workflow_dispatch`).
- Repo settings: Pages source = **GitHub Actions**.
- Pushing: the repo belongs to `NeilLouisThompson`; push as collaborator `thompsonataccso` (remote URL `https://thompsonataccso@github.com/NeilLouisThompson/VillageCrm.git`).
- Live URL: https://neillouisthompson.github.io/VillageCrm/
