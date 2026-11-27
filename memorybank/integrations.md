# Integrations

## External APIs

| Service | Purpose | Auth Method | Notes |
|---------|---------|-------------|-------|
| GitHub Pages | Hosts the static demo site | GitHub Actions OIDC (`pages: write`, `id-token: write`) | Deployed by `.github/workflows/deploy-pages.yml` (TASK-003) |
| Sage Pastel (simulated) | Finance: resident charges, supplier invoices, project journals; resident statements | None (mock) | `IPastelGateway` → `MockPastelGateway`. Month-end lock simulated via `SimulatePastelLocked` (switch on Integrations page) |
| Pilot POS (simulated) | Customer accounts + dietary flag (consent only); till sales | None (mock) | `IPilotGateway` → `MockPilotGateway`. Pilot posts meal/till charges to Pastel itself |
