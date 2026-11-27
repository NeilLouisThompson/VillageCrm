# TASK-002: Restaurant, bar and shop management with booking calendar

## Define
- **Goal**: Add restaurant management alongside Pilot: weekly menu, meal bookings with a calendar for residents and the restaurant, kitchen dietary list, attendance with welfare alerts, meal charges from Pilot (mock), plus bar stock and the trading post (shop) stock.
- **Constraints**:
  - Same architecture as TASK-001: `IRepository<T>` + mock store, rules in services, settings in `appsettings.json` → `VillageOptions`.
  - Pilot stays the POS and posts sales to Pastel itself. Do not duplicate Pilot → Pastel postings.
  - Kitchen sees **dietary flags only**, never diagnoses (POPIA). Welfare alerts only for residents with `WelfareMonitoringConsent`.
  - New role **Restaurant supervisor** (several people may hold it).
  - Booking cut-off is **adjustable by restaurant supervisors** in the app (not only in appsettings).
  - Elderly UX rules apply to resident booking screens (big targets, plain words, no swipe-only).
- **Definition of Done**:
  - [ ] Restaurant supervisor role with its own nav and screens.
  - [ ] Menu per day and sitting, editable by supervisors, shown to residents and on the kiosk.
  - [ ] Residents book and cancel meals from a **month calendar**; the restaurant sees a calendar with booking counts per day and sitting.
  - [ ] Per-sitting kitchen list: booked residents with dietary flags only, printable.
  - [ ] Attendance marking; reception sees a welfare alert when a consenting resident misses meals for N days (configurable).
  - [ ] Bar and shop stock: items, reorder alerts, receive, stock take, kept separate from maintenance stores.
  - [ ] Meal charges from Pilot (mock) appear on the resident statement.
  - [ ] Tests for booking cut-off, capacity, cancel rules, dietary list privacy, welfare alert and the separation of bar/shop stock. Build and tests green.

## Discover
### Findings
- `StockItem` / `StockService` already provide items, min/max, reorder, receive and stock take. Bar and shop can reuse them with a new `Store` field (MaintenanceStores / Bar / Shop) instead of new tables.
- `Resident.DietaryFlag`, `DataSharingConsent` and `WelfareMonitoringConsent` already exist. The kitchen list can use the flag only when consent is given, which is the same rule as `PilotCustomer.From`.
- `IPilotGateway` is push-only. Meal charges need a read side (`GetMealChargesAsync`), or the mock Pastel statement can keep showing the "Dining room (Pilot)" lines it already generates.
- `Access` holds every role rule and the nav in one place, so a new `RestaurantSupervisor` role is a small change.
- The kiosk and the resident home already use the tile pattern, and the calendar can follow it (large day cells).
- Blazor has no built-in calendar component. A simple month grid (7 columns of buttons) is enough and keeps the 56 px target size. No package is needed.

### Affected Files
| File | Role | Notes |
|------|------|-------|
| `Models/Models.cs` | Roles | Add `RestaurantSupervisor` |
| `Models/Restaurant.cs` (new) | Entities | `MenuDay`, `MealBooking`, `RestaurantSettings` |
| `Models/Stock.cs` | Stock | Add `Store` (MaintenanceStores / Bar / Shop) |
| `Services/RestaurantService.cs` (new) | Rules | Menu, bookings, cut-off, capacity, kitchen list, attendance, welfare |
| `Services/StockService.cs` | Rules | Filter by store |
| `Services/Access.cs` | Roles/nav | Restaurant area, resident "Meals" link |
| `Services/VillageOptions.cs` + `appsettings.json` | Settings | Sittings, default cut-off, capacity, welfare days |
| `Integrations/*` | Pilot | Mock meal charges (read) |
| `Components/Shared/MonthCalendar.razor` (new) | UI | Reusable large month grid |
| `Components/Pages/Meals.razor` (new) | Resident | Calendar → day → book/cancel |
| `Components/Pages/RestaurantPage.razor` (new) | Supervisor | Calendar, menu editor, kitchen list, attendance, settings |
| `Components/Pages/StockPage.razor` | Stock | Store tabs; bar/shop visible to restaurant supervisors |
| `Components/Pages/Kiosk.razor`, `Home.razor` | UI | "Meals" tile |
| `Data/Mock/MockSeed.cs` | Seed | Menu, bookings, bar/shop items, a restaurant supervisor |
| `tests/...` | Tests | New `RestaurantTests` |

## Deliver
### Implementation Plan
1. **Role**: add `RestaurantSupervisor` to roles, `Access` (new `Area.Restaurant`), nav and seed (two supervisors).
2. **Models**: `MenuDay { Date, Sitting, Dishes, DietaryOptions }`, `MealBooking { ResidentId, Date, Sitting, Guests, Status (Booked/Cancelled/Attended/NoShow) }`, `RestaurantSettings` entity (single row: cut-off time per sitting, capacity per sitting, open days), editable in the app by supervisors.
3. **RestaurantService**:
   - Book or cancel within the cut-off (supervisors may override); capacity check; one booking per resident per sitting.
   - Month calendar data: residents see their own bookings, supervisors see counts.
   - Kitchen list per sitting: name, unit, guests, dietary flag (only with consent).
   - Attendance; welfare alert when a consenting resident has no attended meal for N days (`WelfareNoMealDays`, default 2). Reception sees it on Census and the dashboard.
4. **Calendar UI**: shared `MonthCalendar` (big day buttons, previous/next month, markers).
   - Resident `/meals`: pick a day → menu → large "Book lunch" / "Cancel" buttons.
   - Supervisor `/restaurant`: calendar with counts → day view (menu editor, kitchen list, attendance), settings panel (cut-off, capacity).
   - Kiosk and resident home get a "Meals" tile. The resident home still has at most 4 main tiles, so "Meals" replaces nothing; it goes into the nav. **Question below.**
5. **Bar and shop stock**: `StockItem.Store`; Stock page tabs. Restaurant supervisors see Bar and Shop; maintenance staff see Maintenance stores; procurement and the village manager see all.
6. **Meal charges**: the mock Pilot meal charges for booked and attended meals feed the existing statement (replacing the random dining lines).
7. **Tests + seed + dashboard rows** (meals booked today, welfare alerts).

### Open questions
1. **Calendar scope**: meals only, or also other resident bookings (hall, guest suite, hairdresser, transport)? Default: **meals only** now, with the calendar built so other booking types can be added later.
2. **Sittings**: which meals are served? Default: **Breakfast, Lunch, Dinner**, configurable.
3. **Bar and shop sales**: recorded in **Pilot** (recommended: the mock Pilot feed reduces stock, with no double entry), or sold in this app and charged to the resident account?
4. **Resident home**: the design caps it at 4 tiles. Replace "Notices" with "Meals" (notices stay in the nav), or keep 4 and put "Meals" in the nav only?

### Approval
- **Status**: ✅ Approved
- **Approved by**: User, 2026-11-06 (answered the open questions)
- **Notes**:
  1. Calendar books **meals only** for now.
  2. Breakfast, lunch and dinner, configurable, **plus special events** (e.g. Christmas lunch) as one-off dated sittings.
  3. No option chosen → using the recommended one: bar and shop sales are recorded in **Pilot**; this app imports them (mock) to reduce stock. Easy to change.
  4. "Keep all": the resident home keeps its 4 tiles and **adds Meals as a 5th** (a deliberate exception to the design's max of 4).

### Changes Made
- `Models/Restaurant.cs` (new): `Sitting` (regular, or a special event when `EventDate` is set; capacity, `CutOffMinutes`, price), `MenuEntry`, `MealBooking` (Booked/Cancelled/Attended/NoShow).
- `Models/Models.cs`: `RestaurantSupervisor` role (appended at the end, because the saved demo role is stored by number). `Models/Stock.cs`: `StockStore` (Maintenance/Bar/Shop) on `StockItem`.
- `Services/RestaurantService.cs` (new): sittings per day, cut-off and open check, book (capacity incl. guests, max guests, no double booking, supervisor override), cancel (own booking, before the cut-off), attendance, covers per day, kitchen list (dietary flag only with consent), welfare alerts, menu and sitting maintenance (supervisor/manager only).
- `Services/StockService.cs`: `ListAsync(store)`, `ImportPilotSalesAsync` (idempotent per sale reference). `Services/Access.cs`: `Area.Restaurant` / `ManageRestaurant`, `StoresFor(role)`, nav links. `DashboardService`: seats today and welfare alerts. `VillageOptions` + `appsettings.json`: `WelfareNoMealDays`, `MaxGuestsPerBooking`.
- `Integrations`: `IPilotGateway.GetSalesAsync` + `PilotSale`; mock Pilot returns stable daily bar/shop sales; mock Pastel statement now shows **attended meals** (price × (1 + guests)) instead of random dining lines.
- UI: shared `MonthCalendar` (large day buttons, badges) and `MealBooker` (day → menu → guest stepper → Book/Cancel); `/meals` (resident), `/restaurant` (supervisor: welfare card, seats calendar, menu editor, printable kitchen list, attendance, book on behalf, meal times/seats/cut-off/price editor, add special event); Meals tile on the resident home (5th tile) and kiosk; Stock page store tabs + Pilot import; Census welfare card; CSS.
- `MockSeed`: 2 restaurant supervisors, Breakfast/Lunch/Dinner + Christmas lunch, 3 weeks of menus, attended and upcoming bookings (Samuel triggers a welfare alert), bar and shop items; job cards only issue maintenance stock.
- Also (TASK-001 request): demo kiosk PIN changed to **0000**.

## Demonstrate
### Test Results
- Build: ✅ 0 errors, 0 warnings
- Tests: ✅ **76/76** (11 new in `RestaurantTests`)
- Browser: ✅ resident home shows 5 tiles; booked dinner + 1 guest from the calendar, and the day badge shows "✔ Lunch, Dinner"; supervisor sees the welfare alert (Samuel, last meal 2 Nov), "5 of 80 seats", cut-off 10:00, and a kitchen list with dietary flags only; Stock shows Bar/Trading post tabs only; Pilot import took 6 lines off stock (lager 30 → 28, wine flagged for reorder).

### Human Verification
- **Status**: ⏳ Awaiting
- **Feedback**:

## Document
- **Summary**:
- **Documentation Updated**:
