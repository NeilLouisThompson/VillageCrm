# TASK-001: Initial Village Management Platform (all modules, mock data layer)

## Define
- **Goal**: Build the first version of the Village Management Platform described in `Retirement_Village_CRM_System_Design.md`. Cover all modules: resident register and movements, job cards, maintenance and projects, contractors, stock and procurement, policy library, compliance calendar, notices, dashboards, plus the Pastel and Pilot integration seams. The database layer is mocked for now and must be easy to replace.
- **Constraints**:
  - Stay on **Blazor Server (.NET 10)**. Extend the existing prototype; do not switch to React.
  - Data access goes through **repository interfaces**. The mock (in-memory) implementations are registered in one place, so a real database implementation can be swapped in by changing one registration line.
  - Pastel and Pilot integrations are **interfaces with mock/no-op implementations only**. No SDK, no network calls.
  - Auth stays mocked (the role picker in the header). No real sign-in in this task.
  - Follow the elderly-UX rules from §5 of the design doc on resident-facing screens.
  - All seed data is invented. Compliance seed data is marked "verify with legal advisor".
- **Definition of Done**:
  - [ ] Every module in §4 has at least one working screen backed by a repository interface.
  - [ ] No page or component references a concrete mock class. Pages depend only on interfaces/services.
  - [ ] Swapping the data layer = replacing one `AddMockDataLayer()` call in `Program.cs`.
  - [ ] Role-based visibility is enforced: residents see only their own records, medical data is visible only to Clinical.
  - [ ] Medical data views are written to an audit log.
  - [ ] xUnit test project covers the mock repositories and key business rules (job lifecycle and reopen, compliance status, contractor expiry block, stock reorder, procurement approval limits).
  - [ ] `dotnet build` and `dotnet test` pass.

## Discover
_(Updated progressively — each section filled in as discovery proceeds)_

### Findings
- **Existing prototype**: Blazor Server (.NET 10, `InteractiveServer` render mode set globally in `App.razor`). No packages beyond the Web SDK. The build is clean (0 warnings).
- **Data access today**: `MockDataService` is a concrete singleton that exposes **public mutable lists** (`Residents`, `JobCards`, `Compliance`). Pages inject it directly. Some pages mutate entities in place (e.g. `ResidentDetail.AddMovement` edits `_resident.Status` and `Movements` directly; `Compliance.razor` reads `Data.Compliance`). **This is the main obstacle to swapping the DB.** Every write must go through a repository method instead.
- **Synchronous API**: all calls are sync. A real DB (EF Core) is async, so the interfaces must be `Task`-based now, or every page would need a rewrite later.
- **Business rules live in pages**: the job next-step state machine is in `JobCards.razor`, and the movement→status mapping is in `ResidentDetail.razor`. These need to move to services so that both the mock and a real DB share them, and so they can be tested.
- **Roles**: `UserRole { Resident, Reception, Clinical, Maintenance, Manager }`. The design needs Facilities manager, Procurement, Finance and System admin too. `ViewerContext` has no "which resident am I" value, so a resident cannot see "my requests".
- **Medical access**: role check in `ResidentDetail.razor` only. No audit log exists.
- **UX**: CSS already has big tiles, 56px-style buttons, pills, and big tables. Body font size is set in `html {}`. There is no emergency banner, no "Bigger text" toggle, and no reception help line on every screen.
- **Tests**: none. No solution file.
- **Gotcha**: a test project inside the repo folder will be picked up by the web project's default `**/*.cs` glob. It must be excluded in `VillageCrm.csproj`.

### Affected Files
| File | Role | Notes |
|------|------|-------|
| `Program.cs` | DI | Replace `AddSingleton<MockDataService>()` with `AddVillageServices()` + `AddMockDataLayer()` |
| `Services/MockDataService.cs` | Mock data | Replaced by `Data/Mock/*` (seed moves to `MockSeed`) |
| `Services/ViewerContext.cs` | Mock auth | Add `ResidentId`, `DisplayName`, `BiggerText` |
| `Models/Models.cs` | Entities | Extend roles, job statuses and fields; new module models in new files |
| `Components/Layout/MainLayout.razor` | Shell | Role-aware nav, emergency banner, help line, bigger text toggle |
| `Components/Pages/*.razor` (6) | UI | Switch from `MockDataService` to interfaces/services |
| `Components/_Imports.razor` | Usings | Add `VillageCrm.Data` |
| `wwwroot/app.css` | Styles | Banner, bigger-text mode, a few new classes |
| `VillageCrm.csproj` | Build | Exclude `tests/**` |
| New: `Data/`, `Data/Mock/`, `Services/*Service.cs`, `Integrations/`, new pages, `tests/VillageCrm.Tests/`, `VillageCrm.slnx` | | See plan |

### Root Cause / Area of Change
This is a greenfield expansion. The core structural change is to put a **repository seam** between the UI/services and the storage, then build each module on top of it.

### External Research
- Relevant docs/updates: Blazor Server on .NET 10. The recommended way to inject services into components is still `@inject`. Async lifecycle methods (`OnInitializedAsync`/`OnParametersSetAsync`) fit async repositories. When EF Core is added later, Blazor Server guidance is to use `IDbContextFactory` (one short-lived context per operation), because the circuit lifetime makes a scoped `DbContext` unsafe. The repository interfaces in this plan fit that pattern.
- Package updates available: no packages are in use. The test project will use the SDK's `xunit` template defaults (SDK 10.0.204).

### Discovery Summary
The app can be extended in place. The data-layer swap requires: (1) async repository interfaces, (2) no direct mutation of entities from pages, and (3) business rules in services, not in pages or repositories. All mock registrations go in one extension method.

## Deliver
### Implementation Plan

**Swap-out design (simplest):**
```
Pages ──> Services (business rules) ──> I*Repository (Data/) ──> Mock*Repository (Data/Mock/, in-memory)
                                                              └─> (later) Ef*Repository (Data/Sql/)
Program.cs:  builder.Services.AddMockDataLayer();   // swap for AddSqlDataLayer(connStr) later
```
- All mock repositories share one singleton `MockStore` (the in-memory lists plus seed data). It plays the role a `DbContext` will play later.
- Repositories do CRUD only. Rules (lifecycle, limits, expiry blocks, SLA) live in services, so the rules survive a DB swap unchanged.

**1. Data layer seam**
- [ ] `Data/` interfaces (all async): `IResidentRepository`, `IJobCardRepository`, `INoticeRepository`, `IAssetRepository` (assets + maintenance schedules), `IContractorRepository`, `IProjectRepository`, `IStockRepository`, `IProcurementRepository`, `IPolicyRepository`, `IComplianceRepository`, `IAuditRepository`, `IIntegrationOutbox`.
- [ ] `Data/Mock/MockStore.cs` (lists + seed, moved from `MockDataService`), `Data/Mock/Mock*Repository.cs`, and `Data/Mock/MockDataLayerExtensions.AddMockDataLayer()`.
- [ ] Delete `Services/MockDataService.cs` once nothing references it.

**2. Models** (new files under `Models/`, one per module; existing `Models.cs` extended)
- [ ] Roles: `Resident, Reception, Clinical, Maintenance, FacilitiesManager, Procurement, Finance, Manager, SystemAdmin`.
- [ ] Resident: add ID number, contract type, occupation date, language, emergency contacts, doctor/hospital/medical aid, DNR/mobility, consent flags (directory, photo, data sharing, welfare monitoring), and pending self-service changes. Movement types: add holiday, deceased and vacated.
- [ ] Job card: add `Closed`/`Reopened` handling, scheduled window, SLA due time, billable flag + amount, stock lines, labour hours, "logged on behalf by".
- [ ] New: `Notice`, `Asset`, `MaintenanceSchedule`, `WorkOrder`, `Contractor` + `ContractorDocument`, `Project` + `Milestone`/`Quote`/`Variation`, `StockItem` + `StockMovement`, `Requisition`/`PurchaseOrder`/`GoodsReceived`/`SupplierInvoice`, `Policy` + `PolicyVersion` + `PolicyAcknowledgement`, `ComplianceItem` (extend: frequency, evidence, escalation), `AuditEntry`, `OutboxMessage`.

**3. Services (business rules, interface-only dependencies)**
- [ ] `ResidentService`: movement → status mapping, medical read with **audit entry**, self-service change → staff verify, consent updates, daily census.
- [ ] `JobCardService`: lifecycle `Logged → Acknowledged → Scheduled → InProgress → Done → resident confirms (Yes = Closed, No = reopened to Acknowledged)`; SLA (Urgent 2 h, Normal 2 working days, Low 5 working days); breach flag for escalation; issue stock to a job; billable job approval → Pastel outbox.
- [ ] `MaintenanceService`: generate due work orders from schedules (daily/weekly/monthly/annual).
- [ ] `ProjectService`: budget vs actual roll-up; contractor appointment **blocked** if fewer than 3 quotes or any compliance doc has expired; variations.
- [ ] `StockService`: issue, receive, stock take adjust, reorder alerts (≤ min).
- [ ] `ProcurementService`: requisition → approval with role/value limits (Facilities manager up to R10,000, Manager up to R100,000, above that Finance + Manager — **configurable in one place, confirm values**) → PO → GRN → 3-way match (qty and price within tolerance) → Pastel outbox.
- [ ] `PolicyService`: Draft → Review → Approved → Superseded (old versions read-only); acknowledge; acknowledgement rates; review date feeds compliance.
- [ ] `ComplianceService`: status (Overdue / Due soon / On track / Acknowledged / Closed), reminder window (30/14/7/1), acknowledge, close with evidence note, escalation flag when overdue.
- [ ] `DashboardService`: occupancy/census, SLA performance, maintenance backlog, project budget vs actual, stock value + reorders, compliance RAG, policy ack rates.
- [ ] `AddVillageServices()` extension registers all services.

**4. Integrations (mock only)**
- [ ] `Integrations/IPastelGateway`, `IPilotGateway` + `MockPastelGateway`, `MockPilotGateway`. Services write to the outbox with a unique external reference (idempotent). An "Integration sync" action processes the outbox through the mock gateways (with a simulated "company locked" retry). Pilot gets only the account number, name, unit, active flag and **dietary flag**, never diagnoses.

**5. Viewer / mock auth & layout**
- [ ] `ViewerContext`: add `ResidentId` (when role = Resident, pick "who am I" in header), `BiggerText` toggle.
- [ ] `MainLayout`: permanent emergency banner, nav filtered by role (resident sees no staff nav), "Need help? Call reception" footer, Bigger text button.

**6. Pages** (existing pages updated, new pages added)
- [ ] `/` Home: resident = 4 large tiles (Report a problem · My requests · Notices · Call reception); staff = role dashboard tiles.
- [ ] `/report`: resident identity pre-filled (3 taps: tile → category → Send). Staff mode keeps the "on behalf of" picker.
- [ ] `/my-requests`: plain-language status messages and "Fixed? Yes / No".
- [ ] `/notices`: residents read; staff create/expire.
- [ ] `/kiosk`: resident flow without login; pick name, then auto-return to start after 2 min of inactivity; no medical data reachable.
- [ ] `/residents`, `/residents/{id}`: profile, contacts, consent, movements, pending self-service changes, medical section (Clinical only, audited). `/my-details` lets a resident edit own contact details (pending verification).
- [ ] `/census`: daily census.
- [ ] `/job-cards`: lifecycle actions, schedule window, SLA badge, stock used, billable approval.
- [ ] `/assets`: asset register, schedules, "Generate due work orders".
- [ ] `/contractors`: register + document expiry status.
- [ ] `/projects`, `/projects/{id}`: budget vs actual, milestones, quotes, appoint contractor (with block reason), variations.
- [ ] `/stock`: items, reorder alerts, issue/receive/stock take.
- [ ] `/procurement`: requisitions, approvals, POs, GRNs, invoice match.
- [ ] `/policies`: library, versions, acknowledge; resident large-text view.
- [ ] `/compliance`: reminders, acknowledge, close with evidence, escalation.
- [ ] `/dashboard`: KPIs + **CSV export** (opens in Excel).
- [ ] `/integrations`: outbox, process/retry, reconciliation view.
- [ ] `/audit`: audit log (System admin / Manager only).

**7. Tests**
- [ ] `tests/VillageCrm.Tests` (xUnit), `VillageCrm.slnx`, and `<Compile Remove="tests/**" />` (plus Content/None) in `VillageCrm.csproj`.
- [ ] Tests: job lifecycle + reopen + SLA breach; movement → status; medical read writes an audit entry; contractor block (expired doc, < 3 quotes); stock reorder + issue; procurement approval limits + 3-way match; policy versioning supersede; compliance status/reminder windows; maintenance work order generation; outbox idempotency; mock repository CRUD.

**Explicitly deferred (not in this task, proposed as follow-up tickets):**
- Real database (EF Core + SQL Server `AddSqlDataLayer`), real auth (Entra ID / PIN / QR), PWA manifest + service worker, English/Afrikaans i18n, photo/voice note upload, SMS/email, PDF export (browser print is enough for now), real Pastel/Pilot SDK/API.

**Verify loop**: `dotnet build` + `dotnet test` after each numbered section.

### Approval
- **Status**: ✅ Approved
- **Approved by**: User, 2026-11-06
- **Notes**: Proposed limits accepted for now. Make all of them configurable: approval limits, SLA, reminder days, retention, minimum quotes, labour rate, kiosk timeout and phone numbers go in `appsettings.json` → `Village` section (`VillageOptions`).

### Changes Made
- **Deviation from plan (simplification):** a single generic `IRepository<T>` (`Data/IRepository.cs`) is used instead of ~12 per-module repository interfaces. One generic EF implementation will later replace `MockRepository<T>`, so the swap is smaller. Module-specific queries live in services.
- `Data/IRepository.cs`, `Data/Mock/MockStore.cs` (in-memory tables that return **copies**, so a change only sticks after `UpdateAsync`, as with a real database), `Data/Mock/MockRepository.cs`, `Data/Mock/MockDataLayerExtensions.cs` (`AddMockDataLayer()`), `Data/Mock/MockSeed.cs` (all seed data).
- `Models/Models.cs` (rewritten: roles, resident, medical record split out, consent, movements, notices, audit, frequency), plus new `Models/Jobs.cs`, `Models/Operations.cs` (assets, work orders, contractors, projects), `Models/Stock.cs` (stock, purchases), `Models/Governance.cs` (policies, compliance, outbox).
- `Services/`: `VillageOptions` (all settings), `Access` (role rules + nav), `Format`, `AuditAndOutbox`, `ResidentService`, `JobCardService`, `StockService`, `ProcurementService`, `MaintenanceService` (+ `ContractorService`, `NoticeService`), `ProjectService`, `PolicyAndComplianceService`, `DashboardService`, `VillageServiceExtensions` (`AddVillageServices()`); `ViewerContext` extended (name, resident id, bigger text).
- `Integrations/Gateways.cs` (`IPastelGateway`, `IPilotGateway`, `PilotCustomer` sends a dietary flag only, and only with consent) and `Integrations/MockGateways.cs` (`AddMockIntegrations()`).
- `appsettings.json` (new): `Village` section with every limit and rule.
- `Program.cs`: `AddMockDataLayer()`, `AddMockIntegrations()`, `AddVillageServices()`, plus the `/export/dashboard.csv` endpoint (CSV injection-safe).
- `VillageCrm.csproj`: excludes `tests/**`. New `VillageCrm.slnx`.
- Deleted `Services/MockDataService.cs`.
- UI: `MainLayout` (emergency banner, role-filtered nav, resident picker, Bigger text, help footer), new `KioskLayout`, `ViewerAwarePage` (`TryAsync` friendly errors, reload on viewer change), shared `RoleGate`, `ErrorMessage`, `ReportFlow`, `RequestList`, `NoticeList`.
- Pages (existing rewritten; files renamed with a `Page` suffix where the class name clashed with an injected service, routes unchanged): Home, Report, MyRequests, MyDetails, Directory, Notices, Kiosk, Residents, ResidentDetail, Census, JobCards, Assets, Contractors, Projects, ProjectDetail, Stock, Procurement, Policies, Compliance, Dashboard, Integrations, Audit.
- `wwwroot/app.css`: banner, bigger text (zoom), stats, inline forms, print styles.
- `tests/VillageCrm.Tests` (xUnit): `TestApp` (real DI graph, empty mock store, fixed clock), plus 6 test classes.

## Demonstrate
### Test Results
- Build: ✅ `dotnet build`: 0 errors, 0 warnings
- Tests: ✅ `dotnet test VillageCrm.slnx`: **58 passed**, 0 failed, 0 skipped
- Smoke test: ✅ ran the app; all 24 routes (every page + `/export/dashboard.csv`) returned HTTP 200 with no exceptions in page bodies or logs.
- Output: covered rules include the mock repository copy semantics; the job lifecycle with reopen, invalid transitions, SLA breach and working days; charge approval to Pastel; stock costing on jobs; movement → status; the clinical-only medical gate plus its audit entry; Pilot dietary flag needing consent and never carrying diagnoses; contact change verification; census; the contractor block (expired or missing documents, fewer than the minimum quotes); retention; variations; work order generation; weighted average cost; stock take; approval limits (configurable); Manager + Finance approval for large amounts; the 3-way match and short delivery; policy supersede, review booking and acknowledge-once; compliance states and reminders; close with evidence creating the next occurrence; outbox idempotency and locked retry; CSV formula escaping.

### Human Verification
- **Status**: 🔄 Iteration needed (awaiting user decision on findings)
- **Browser walkthrough (2026-11-06)**: ✅ resident 3-tap report; My requests "not fixed" reopens; Bigger text; contact change → reception accept → applied; hospital admission → status + census; job acknowledge/schedule (Fri + 2 working days = Tue); escalation badge; stock issue deducts; charge proposed → Facilities manager approves → Pastel outbox; contractor appoint blocked at 2 quotes; R117k purchase needs Manager + Finance (Facilities manager gets no button); clinical medical view audited and hidden on role switch; outbox sends; dashboard RAG; kiosk has no staff nav.
- **Findings**:
  1. Bug: two quotes from the **same contractor** count toward the 3-quote minimum. Should count distinct contractors.
  2. Wording: job card says "approved and sent to Pastel" while the charge is only queued until the outbox runs.
  3. Expected (mock auth): a full page reload resets the role to Reception (role lives in the browser connection).
- **Feedback**: User: "fix all known issues, put the dropdown in the corner, make it smaller."
- **Repair loop 1 (2026-11-06)**:
  1. `ProjectService.AppointAsync` counts **distinct contractors** (`DistinctQuoteCount`); the message and the Quotes heading were updated. New test `Repeat_quotes_from_the_same_contractor_do_not_count_towards_the_minimum`; existing appointment test adjusted.
  2. Job card wording → "approved and queued for Pastel".
  3. Role survives reloads: `MainLayout` saves `{Role, ResidentId}` in `ProtectedLocalStorage` (encrypted, built into Blazor Server) and restores it on first render. Pages render only after the restore, so audit entries are never written under the default role.
  4. Role picker moved to a small fixed panel in the bottom-right corner (`.demo-viewer`), hidden when printing. The header keeps the name and Bigger text.
  5. Found during re-test: wide tables in `.grid` cards overflowed under the neighbouring card (Appoint button not clickable on narrow screens). Fixed with `minmax(min(300px,100%),1fr)`, `min-width:0` and `overflow-x:auto` on cards.
  - Build ✅ 0 warnings · Tests ✅ **59/59** · Browser ✅ role kept after reload and after a server restart; appoint blocked with "3 quotes from different contractors … (there are 2)"; corner picker confirmed by screenshot.
- **Scope change requested (2026-11-06)**: "Residents should be able to see their account and the items. On the Kiosk we need a search." ✅ Approved ("Everything": A, B, and C with a PIN). Delivered:
  - A. `IPastelGateway.GetStatementAsync` → `AccountStatement(Balance, Lines)`; the mock combines levies, payments and dining charges with job charges actually posted through the outbox. Shared `AccountStatementView` on My details, the resident record (Reception / Village manager) and the kiosk.
  - B. Kiosk search box (name or unit).
  - C. Kiosk "My account" behind a 4-digit PIN with a large on-screen keypad. The PIN is stored as a salted PBKDF2 hash (`PinHasher`); after 3 wrong tries the session ends; wrong tries are audited. Reception sets the PIN on the resident record. Demo seed PIN: 0000 (changed from 1234 at the user's request).
  - Build ✅ · Tests ✅ **65/65** (new `AccountAndKioskTests`) · Browser ✅ search "b-" → 2 matches; wrong PIN → "Tries left: 2"; correct PIN shows the statement.
- Restaurant, bar, shop and booking calendar → split into **TASK-002** (new module).
  - A. Account statement: replace `IPastelGateway.GetAccountBalanceAsync` with `GetStatementAsync(account)` returning a balance + lines (date, description, reference, amount). The mock produces levies, dining room charges and payments, plus job charges it has actually received from the outbox. Shown on My details (resident) and the resident record (reception, on the resident's behalf).
  - B. Kiosk: a large search box (name or unit) above the name tiles.
  - C. Privacy: the kiosk is shared and has no PIN, so showing the account there would expose finances to anyone. Options: no account on kiosk, or a mock 4-digit PIN before the account is shown.

## Document
- **Summary**:
- **Documentation Updated**:
