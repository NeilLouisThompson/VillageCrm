# Features

## Active Features

| Feature | Description | Status |
|---------|-------------|--------|
| Public demo site | Runs fully in the browser (Blazor WebAssembly) at https://neillouisthompson.github.io/VillageCrm/. Each visitor gets their own sample data, which resets on refresh. See [components/static-hosting.md](components/static-hosting.md). | ✅ Live (TASK-003) |
| Demo role switcher | "Demo role" selector; choice is remembered in browser `localStorage` (key `demo-viewer`). | ✅ (TASK-003: moved from server-protected storage) |
| Dashboard CSV export | "Export to Excel (CSV)" builds the file in the browser and downloads `village-dashboard-yyyyMMdd.csv`. Aggregate figures only. | ✅ (TASK-003: moved from server endpoint) |
