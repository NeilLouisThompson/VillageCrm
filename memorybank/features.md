# Features

## Active Features

| Feature | Description | Status |
|---------|-------------|--------|
| Public demo site | Runs fully in the browser (Blazor WebAssembly) at https://neillouisthompson.github.io/VillageCrm/. Each visitor gets their own sample data, which resets on refresh. See [components/static-hosting.md](components/static-hosting.md). | ✅ Live (TASK-003) |
| Demo role switcher | "Demo role" selector; choice is remembered in browser `localStorage` (key `demo-viewer`). Changing role or resident navigates to that role's start page (`Access.StartPage`). See [components/demo-role-switcher.md](components/demo-role-switcher.md). | ✅ (TASK-003, TASK-004) |
| Dashboard CSV export | "Export to Excel (CSV)" builds the file in the browser and downloads `village-dashboard-yyyyMMdd.csv`. Aggregate figures only. | ✅ (TASK-003: moved from server endpoint) |
| Pastel and Pilot integration (simulated) | Integrations page with flow overview, outbox (animated send, month-end lock switch) and simulated Pastel/Pilot screens; notices when messages are queued, received or fail. Finance, Village manager, System admin. See [components/pastel-pilot-integration.md](components/pastel-pilot-integration.md). | ✅ (TASK-005) |
