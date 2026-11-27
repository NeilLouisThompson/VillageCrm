# Pastel and Pilot Integration (simulated)

## Overview
Village Care sends postings to Sage Pastel and customer accounts to Pilot POS through an outbox, and reads statements and till sales back. In the demo both systems are simulated in the browser, and the Integrations page shows the whole flow.

## Business Rules
- Every outbound message has a unique reference per target; queuing the same reference twice is ignored.
- Messages go Pending → Sent, or Failed when the system is unavailable (e.g. Pastel month-end lock). Failed messages are retried.
- Pilot only gets name, unit, active flag and the dietary flag **with consent**. Never medical data.
- Pilot posts meal and till charges to Pastel itself; Village Care does not.
- Integrations page: Finance, Village manager, System admin.

| Type | Target | Reference | Started by |
|------|--------|-----------|-----------|
| DebtorCharge | Pastel | `JOB-{id}-CHARGE` | Job cards: approve charge |
| SupplierInvoice | Pastel | `PUR-{id}-INV` | Procurement: invoice matched |
| ProjectCostJournal | Pastel | `PRJ-{id}-COST-{n}` | Projects: add cost/certificate |
| CustomerAccount | Pilot | `PILOT-{account}-{guid}` | Residents: add, edit, consent, movement |

## Data Flow

```
Service → OutboxService.EnqueueAsync → IntegrationActivity(Queued) → IntegrationToasts (notice)
        ≈3 s → DemoOutboxSender → OutboxService.SendAsync → Mock gateway → IntegrationActivity(Sent/Failed)
Integrations page ← OutboxService.ListAsync, MockExternalSystems (built from sent messages + attended meals + Pilot sales)
```

### Step-by-step:
1. A business action queues a message (`EnqueueAsync`); a "Queued for …" notice shows.
2. `IntegrationToasts` (in `MainLayout`) waits ~3 s, then `DemoOutboxSender` sends pending messages one at a time (800 ms pause each, raising `Sending`).
3. The gateway accepts (notice "… received") or throws `IntegrationUnavailableException` (notice "… not available … will be retried").
4. "Send now" or switching the lock off retries Failed messages.

## Validation Rules

| Field | Rule | Where Enforced | Error Message |
|-------|------|----------------|---------------|
| Reference | Unique per target | `OutboxService.EnqueueAsync` | None (returns false) |
| Dietary flag to Pilot | Only with `DataSharingConsent` | `PilotCustomer.From` | None (sent as null) |

## Key Files

| File | Role |
|------|------|
| `Services/AuditAndOutbox.cs` | `OutboxService` |
| `Services/IntegrationActivity.cs` | Events + friendly type labels |
| `Integrations/Gateways.cs` | `IPastelGateway`, `IPilotGateway`, `PilotCustomer` |
| `Integrations/MockGateways.cs` | Mock gateways and DI registration |
| `Integrations/MockExternalSystems.cs` | Simulated Pastel/Pilot data (demo only) |
| `Integrations/DemoOutboxSender.cs` | Simulated background sender (demo only) |
| `Components/Pages/Integrations.razor` | Tabs: How it works, Outbox, Pastel, Pilot |
| `Components/Shared/IntegrationToasts.razor` | Notices + auto-send |
| `Data/Mock/MockSeed.cs` | Sent Pilot accounts and LED project journal at start |

## Edge Cases & Gotchas
- `MockExternalSystems`, `DemoOutboxSender` and `IntegrationToasts` are demo-only. A real system sends from a server-side worker; remove them when real gateways are added.
- The Pilot customer list shows the **latest** sent `CustomerAccount` per account.
- `SimulatePastelLocked` is changed at runtime through `IOptionsMonitor<VillageOptions>.CurrentValue` (resets on reload).
- Only pages deriving from `ViewerAwarePage` get `Dispose`; it is virtual so pages can unsubscribe from extra events.

## Related Components
- [static-hosting.md](static-hosting.md)
- [demo-role-switcher.md](demo-role-switcher.md)
