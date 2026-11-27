# TASK-005: Mock the Pastel and Pilot integration flow end to end

## Define
- **Goal**: Make the external integrations (Sage Pastel, Pilot POS) visible in the demo, so a viewer can see what is sent where, when, and what each external system "has", including failure and retry.
- **Constraints**:
  - Still fully mocked and browser-only (static GitHub Pages hosting). No real connections.
  - Keep the `IPastelGateway` / `IPilotGateway` interfaces and the outbox pattern, so real gateways can still replace the mocks with one line in `Program.cs`.
  - Privacy rules unchanged: Pilot only gets the dietary flag with consent, and never medical data.
  - User asked for the "full" mock (all options offered): flow diagram, mock Pastel/Pilot screens, live send with notifications, on-screen month-end lock switch, seeded messages, Village manager access.
- **Definition of Done**:
  - The Integrations page has: a flow diagram, the outbox with an animated send, a Pastel lock switch, a mock Pastel screen and a mock Pilot screen.
  - Actions that queue a message show a notification, and the message is auto-sent a few seconds later (or fails while Pastel is locked, then retries when unlocked).
  - The page is not empty on first load.
  - The Village manager can open Integrations.
  - Build and all tests pass (new tests for new behaviour); verified on the live site.

## Discover

### Findings
- `Integrations/Gateways.cs`: `IPastelGateway` (`SendAsync`, `GetStatementAsync`), `IPilotGateway` (`SendAsync`, `GetSalesAsync`), `PilotCustomer.From` (dietary flag only with consent).
- `Integrations/MockGateways.cs`: the mock Pastel keeps posted `DebtorCharge`s in a private bag (used for statements) and throws `IntegrationUnavailableException` when `VillageOptions.SimulatePastelLocked`. The mock Pilot only logs; sales are generated per day.
- `Services/AuditAndOutbox.cs` `OutboxService`: `EnqueueAsync` (idempotent by reference), `ProcessAsync` (sends all non-sent, marks Failed on `IntegrationUnavailableException`), `ListAsync`. No events.
- Messages queued by the app:
  | Target | Type | Reference | From |
  |--------|------|-----------|------|
  | Pastel | DebtorCharge | `JOB-{id}-CHARGE` | Job card charge approved |
  | Pastel | SupplierInvoice | `PUR-{id}-INV` | Purchase invoice matched |
  | Pastel | ProjectCostJournal | `PRJ-{id}-COST-{n}` | Project cost/certificate |
  | Pilot | CustomerAccount | `PILOT-{account}-{guid}` | Resident added/edited/consent/movement |
- Reads: Pastel → statements (resident account, kiosk); Pilot → till sales imported to bar/shop stock. Pilot posts meal and till charges to Pastel itself.
- `Components/Pages/Integrations.razor`: counts and a table, a "Send waiting messages now" button, and help text telling users to edit `appsettings.json` for the lock (impossible on the static site). Only Finance and System admin can see it.
- `MockSeed` seeds **no** outbox messages, so the page is empty on first load.
- `SimulatePastelLocked` can be changed at runtime via `IOptionsMonitor<VillageOptions>.CurrentValue` (the existing test does this).
- CSS: no tabs/toast/diagram styles yet; colour variables `--brand`, `--accent`, `--ok`, `--warn`, `--bad`.

### Affected Files
| File | Role | Notes |
|------|------|-------|
| `Services/AuditAndOutbox.cs` | Outbox | Per-message `SendAsync`; raise activity events |
| `Services/IntegrationActivity.cs` | New | Singleton event hub: queued / sent / failed |
| `Services/VillageServiceExtensions.cs` | DI | Register `IntegrationActivity` |
| `Integrations/MockExternalSystems.cs` | New | Read model of what mock Pastel/Pilot "hold" |
| `Integrations/MockGateways.cs` | DI | Register `MockExternalSystems` |
| `Components/Pages/Integrations.razor` | Page | Tabs: How it works / Outbox / Pastel (mock) / Pilot (mock); lock switch |
| `Components/Shared/IntegrationToasts.razor` | New | Notifications + simulated background sender |
| `Components/Layout/MainLayout.razor` | Layout | Host `IntegrationToasts` |
| `Services/Access.cs` | Rules | Integrations: add Village manager |
| `Data/Mock/MockSeed.cs` | Seed | Sent Pilot customer accounts + LED project cost journal |
| `wwwroot/app.css` | Styles | Tabs, flow diagram, toasts |
| `tests/...` | Tests | New behaviour |

### Root Cause / Area of Change
The mocks work, but their side of the flow is invisible and can't be controlled from the UI.

### External Research
- Relevant docs/updates: N/A (internal UI and mock behaviour; no new packages).
- Package updates available: none needed.

### Discovery Summary
UI-heavy but low-risk. Business services are unchanged except `OutboxService` (extract a per-message send and raise events). The mock "external screens" are derived from **sent** outbox messages, so seeded and live data stay consistent.

## Deliver
### Implementation Plan
- [x] 1. `IntegrationActivity` (singleton): `event Action<OutboxMessage, string>? Changed` with kinds Queued / Sent / Failed. `OutboxService` raises it in `EnqueueAsync` and in a new public `SendAsync(OutboxMessage)` (extracted from `ProcessAsync`, which now loops over it; behaviour unchanged).
- [x] 2. `MockExternalSystems` (mock-only, registered in `AddMockIntegrations`):
  - **Pastel**: postings from **sent** Pastel messages: Debtors (job charges), Creditors (supplier invoices), General ledger (project cost journals), plus meal charges posted by Pilot (attended bookings).
  - **Pilot**: customer accounts (latest sent `CustomerAccount` per account: name, unit, active, dietary flag or "not shared") and today's till sales (`GetSalesAsync`, with item names).
- [x] 3. Integrations page with 4 tabs:
  - **How it works**: a CSS diagram showing Village Care → Pastel (charges, invoices, journals), Village Care → Pilot (customer + dietary flag with consent), Pilot → Pastel (meal/till charges), Pastel → Village Care (statements, read-only), Pilot → Village Care (till sales → stock). Each arrow names the screen that triggers it.
  - **Outbox**: existing counts and table; "Send now" sends one message at a time with a short "Sending…" highlight; a **Simulate Pastel month-end lock** switch (turning it off retries failed messages).
  - **Pastel (mock)** and **Pilot (mock)**: styled as the external system, showing the data above.
  - Help text no longer mentions `appsettings.json`.
- [x] 4. `IntegrationToasts` in `MainLayout`: on Queued, show "Queued for Pastel: Debtor charge JOB-3-CHARGE". About 3 seconds later, auto-send pending messages (simulated background worker) and show "Pastel received …" or "Pastel is locked (month-end): will retry". Toasts fade after about 6 seconds and include a "See integrations" link for roles that can open it.
- [x] 5. Seed: sent `CustomerAccount` for each resident (Pilot customer list is populated) and a sent `ProjectCostJournal` `PRJ-2-COST-1` for the LED project's Certificate 1 (R 20,000).
- [x] 6. `Access`: Integrations allowed for Finance, Village manager, System admin.
- [x] 7. CSS for tabs, diagram and toasts (existing colour variables).
- [x] 8. Tests: activity events fire on queue/send/fail; Pastel mock view shows only sent postings; Pilot mock view shows the latest account and hides the dietary flag without consent; the Village manager can access Integrations.
- [x] 9. Publish and verify locally, then push and verify on the live site.

### Approval
- **Status**: ✅ Approved
- **Approved by**: User (2026-11-27)
- **Notes**:

### Changes Made
- `Services/IntegrationActivity.cs` (new) — Queued/Sending/Sent/Failed events, friendly message labels
- `Services/AuditAndOutbox.cs` — `OutboxService.SendAsync(message)` extracted from `ProcessAsync`; raises events; `ListAsync(status?)`
- `Services/VillageServiceExtensions.cs` — registers `IntegrationActivity`
- `Integrations/MockExternalSystems.cs` (new) — simulated Pastel postings, Pilot customers, till sales
- `Integrations/DemoOutboxSender.cs` (new) — demo background sender, one message at a time with a pause
- `Integrations/MockGateways.cs` — registers the two demo services
- `Components/Pages/Integrations.razor` — 4 tabs, lock switch, animated send
- `Components/Shared/IntegrationToasts.razor` (new) + `MainLayout.razor` — notices and auto-send
- `Components/ViewerAwarePage.cs` — `Dispose` made virtual (page unsubscribes from events)
- `Services/Access.cs` — Integrations also for Village manager
- `Data/Mock/MockSeed.cs` — sent Pilot customer accounts and LED project journal
- `wwwroot/app.css` — tabs, flow map, simulated system frames, toasts
- `tests/.../GovernanceTests.cs` — 2 new tests

## Demonstrate
### Test Results
- Build: ✅
- Tests: ✅ 79/79 (2 new: outbox events; simulated systems show only received data, latest Pilot account wins, dietary flag hidden without consent)
- Output: Browser check on a Pages-style local build as Village manager: flow tab lists 7 flows with live counts (Pilot 8, journals 1); Pastel shows General ledger + Debtors (Pilot meals); Pilot shows 8 customers ('Not shared (no consent)' where applicable) and 6 till sales. Saving consent → 'Queued for Pilot' then 'Pilot received' (~3s). With the lock on, adding a project cost → 'Pastel not available… will be retried'; unlocking → 'Sending…' row, '1 sent, 0 failed', 'Pastel received', and the cost appears in Pastel's ledger. No page errors.

### Human Verification
- **Status**: ✅ Confirmed (2026-11-27)
- **Feedback**: User: "Perfect thanks".

## Document
- **Summary**: Integrations page rebuilt with flow overview, outbox (animated send, Pastel month-end lock switch), and simulated Pastel/Pilot screens. Notices and a demo background sender show each message being queued, received or failing. Seeded sent messages; Village manager can open Integrations. `OutboxService` gained a per-message `SendAsync` and activity events; `ViewerAwarePage.Dispose` made virtual.
- **Documentation Updated**:
  - `features.md`: integration row
  - `architecture.md`: modules and data flow
  - `integrations.md`: Pastel and Pilot (simulated)
  - `components/pastel-pilot-integration.md`: new; `static-hosting.md`: related links
  - `README.md`: component doc links
  - `setup.md`, `project-context.md` (placeholder), `changelog.md`: N/A
  - Alignment check: features, architecture and component docs agree
