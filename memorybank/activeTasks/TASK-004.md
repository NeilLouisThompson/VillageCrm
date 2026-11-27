# TASK-004: Role switch goes to that role's start page

## Define
- **Goal**: When the demo role (or the selected resident) changes, the navigation shows that role's links and the user is taken to a sensible start page for that role, instead of staying on a page they may not be allowed to see.
- **Constraints**: Demo-only role switcher (no real sign-in). Keep role rules in one place (`Access`). Restoring the saved role on page load must **not** redirect (deep links must keep working).
- **Definition of Done**:
  - Changing the role or the resident navigates to the role's start page.
  - Every start page is a page that role is allowed to see.
  - Build and all tests pass; verified on the live site.

## Discover

### Findings
- Checked on the live site: the nav **already updates** on role change (`MainLayout` re-renders on `ViewerContext.Changed`), but the user stays on the current URL. For example, Maintenance on `/residents` sees "This page is for: Reception, Clinical, Village manager".
- `MainLayout.OnRoleChanged` / `OnResidentChanged` set the viewer and save to `localStorage`; there is no navigation.
- `OnAfterRenderAsync(firstRender)` restores the saved role. This must stay redirect-free.
- `Access.NavLinks(role)` is the single source of role-to-link rules. `GovernanceTests` already tests access rules.

### Affected Files
| File | Role | Notes |
|------|------|-------|
| `Services/Access.cs` | Role rules | Add `StartPage(UserRole)` |
| `Components/Layout/MainLayout.razor` | Role switcher | `NavigationManager.NavigateTo(Access.StartPage(...))` after a user change |
| `tests/VillageCrm.Tests/GovernanceTests.cs` | Tests | Start page is allowed for every role |

### Root Cause / Area of Change
The role switcher changes state but never navigates.

### External Research
- Relevant docs/updates: `NavigationManager.NavigateTo` resolves relative URIs against `<base href>`, so relative start pages work under `/VillageCrm/`.
- Package updates available: N/A

### Discovery Summary
Small change: one mapping, two calls in `MainLayout`, one test.

## Deliver
### Implementation Plan
- [x] 1. `Access.StartPage(UserRole)`:

  | Role | Start page |
  |------|-----------|
  | Resident | Home (resident tiles) |
  | Reception | Job cards |
  | Clinical | Census |
  | Maintenance | Job cards |
  | Facilities manager | Maintenance (assets) |
  | Procurement | Procurement |
  | Finance | Integrations |
  | Village manager | Dashboard |
  | System admin | Integrations |
  | Restaurant supervisor | Restaurant |
- [x] 2. `MainLayout`: after `OnRoleChanged` / `OnResidentChanged` save, `Navigation.NavigateTo(Access.StartPage(Viewer.Role))`. No change to the restore-on-load path.
- [x] 3. Test: for every role, `StartPage` is `./` or one of that role's `NavLinks`.

### Approval
- **Status**: ✅ Approved
- **Approved by**: User (2026-11-27)
- **Notes**:

### Changes Made
- `Services/Access.cs` — `StartPage(UserRole)`
- `Components/Layout/MainLayout.razor` — navigate to `StartPage` after a role or resident change (not on restore)
- `tests/VillageCrm.Tests/GovernanceTests.cs` — `Every_role_starts_on_a_page_it_may_see`

## Demonstrate
### Test Results
- Build: ✅
- Tests: ✅ 79/79 (incl. new start-page test)
- Output: Browser check on a Pages-style local build: every role lands on its start page and none shows the 'This page is for' gate; resident switch → Home; deep link `/residents` is not redirected on load; reload keeps the role. No page errors.

### Human Verification
- **Status**: ⏳ Awaiting
- **Feedback**:

## Document
- **Summary**:
- **Documentation Updated**:
