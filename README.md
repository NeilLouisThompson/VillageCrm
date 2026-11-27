# Village Care (Demo)

A mock Blazor application for the retirement village design. It uses .NET 10,
Blazor Web App with interactive server rendering, and in-memory sample data.
All names and records are invented.

## Run it

1. Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
2. In this folder, run:

       dotnet run

3. Open the address shown in the console (for example https://localhost:7xxx).

## What is included

| Page | What it shows |
|---|---|
| Home | Large, simple buttons for the main tasks |
| Report a problem | Three-step resident flow: name, problem type, details |
| Job cards | Staff view: acknowledge, schedule, start and close job cards, with history |
| Residents | Searchable list of residents |
| Resident detail | Profile, movement log (with recording), job cards. Medical record shown only to the Clinical role |
| Compliance | Sample legal deadlines, with acknowledgement and status colours |

Use the "Viewing as" selector in the top bar to switch roles and see how
access changes. Medical data is only shown to Clinical.

## Notes

- This is a demo. Sign-in is simulated by the role selector, and there is no database.
- Data is shared across all users and resets when the app restarts.
- Replace MockDataService with a real repository (for example SQL Server with
  Entity Framework Core) when moving on to the real build.
- Compliance items are examples only. Confirm real obligations with a legal adviser.

## Development Workflow (D5)

All development follows the **D5 agentic workflow** with continuous progress tracking.

| Command | Description |
|---------|-------------|
| `StartTask TASK-XXXX` | Start a new task through the D5 phases |
| `ReviewTasks` | Review all incomplete tasks and resume where you left off |

### Agent Configuration

| File | Purpose |
|------|--------|
| [.github/copilot-instructions.md](.github/copilot-instructions.md) | D5 workflow definition, task tracking, phase gates |
| [agents.md](agents.md) | Behavioral guidelines — think first, simplicity, surgical changes |
| [memorybank/project-context.md](memorybank/project-context.md) | Tech stack rules, coding conventions, anti-patterns |

### Documentation (Memory Bank)

| File | Contents |
|------|----------|
| [memorybank/architecture.md](memorybank/architecture.md) | Solution structure, layers, key design decisions |
| [memorybank/features.md](memorybank/features.md) | Feature list and high-level behavior |
| [memorybank/integrations.md](memorybank/integrations.md) | External services and APIs |
| [memorybank/setup.md](memorybank/setup.md) | Local development setup |
| [memorybank/ticket-progress.md](memorybank/ticket-progress.md) | Status tracker for all tickets across sessions |
| [memorybank/changelog.md](memorybank/changelog.md) | Ad-hoc changes outside task files |
| [memorybank/components/](memorybank/components/) | Per-feature documentation (business rules, data flow, validation) |

### Component Documentation

| File | Contents |
|------|----------|
| [memorybank/components/_index.md](memorybank/components/_index.md) | Template and conventions for component docs |
