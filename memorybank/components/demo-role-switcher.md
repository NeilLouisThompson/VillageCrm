# Demo Role Switcher

## Overview
Stand-in for real sign-in: the "Demo role" selector (bottom right) chooses who you are, and for residents which resident.

## Business Rules
- The choice is saved in `localStorage` (key `demo-viewer`, `{ Role, ResidentId }`) and restored on load.
- Changing role or resident navigates to the role's start page (`Access.StartPage`); restoring on load does **not** navigate, so deep links work.
- Start pages: Resident → Home; Reception, Maintenance → Job cards; Clinical → Census; Facilities manager → Maintenance; Procurement → Procurement; Finance, System admin → Integrations; Village manager → Dashboard; Restaurant supervisor → Restaurant.
- Every start page must be one the role may see (tested in `GovernanceTests`).

## Data Flow

```
Select change → MainLayout.OnRoleChanged/OnResidentChanged → ViewerContext.Set… → localStorage → NavigationManager.NavigateTo(Access.StartPage)
```

## Validation Rules

| Field | Rule | Where Enforced | Error Message |
|-------|------|----------------|---------------|
| Saved role | Must deserialize | `MainLayout.LoadSavedAsync` | None (falls back to default) |

## Key Files

| File | Role |
|------|------|
| `Components/Layout/MainLayout.razor` | Selector, save/restore, navigation |
| `Services/ViewerContext.cs` | Current viewer |
| `Services/Access.cs` | Role rules, `NavLinks`, `StartPage` |

## Edge Cases & Gotchas
- Start pages are base-relative (`job-cards`, `./`) because the site runs under `/VillageCrm/`.

## Related Components
- [static-hosting.md](static-hosting.md)
- [pastel-pilot-integration.md](pastel-pilot-integration.md)
