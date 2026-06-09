<!-- AUTOREADME:START -->
<p align="center">
  <h1>🌾 PitaSmart</h1>
  <p>Offline-first SaaS for Ecuadorian farmers: Chemical traceability + Cost/income management</p>
</p>

<p align="center">
  <a href="https://github.com/FaiCP/PitaCost/stargazers"><img src="https://img.shields.io/github/stars/FaiCP/PitaCost?style=flat&color=yellow" alt="Stars" /></a>
  <a href="https://github.com/FaiCP/PitaCost/commits"><img src="https://img.shields.io/github/last-commit/FaiCP/PitaCost?style=flat" alt="Last Commit" /></a>
</p>

## Overview

**PitaSmart** solves two critical problems for rural farmers with intermittent connectivity:

1. **Chemical Traceability (Agrocalidad)** — Enforce mandatory carencia periods (safety waiting time between chemical application & harvest) per Ecuadorian regulations
2. **Profitability Dashboard** — Track costs/income per plot in real-time, with offline-first sync

## Tech Stack

| Layer | Tech |
|-------|------|
| **Frontend** | Angular 18+, PWA, RxDB (offline IndexedDB), Angular Material |
| **Backend** | .NET 9, Clean Architecture, CQRS + MediatR |
| **Database** | SQL Server 2022, RowVersion for optimistic sync |
| **Auth** | Passkeys (WebAuthn/Fido2), JWT fallback |
| **Realtime** | SignalR (market prices) |
| **Reporting** | jsreport (official Agrocalidad PDFs) |

## Key Features

- ✅ **Offline-First** — Register applications/harvests without connectivity; auto-sync when online
- ✅ **Carencia Validation** — Double-validated (client + server) to prevent unsafe harvests
- ✅ **Profitability Analytics** — KPIs by plot with cache-aside pattern for offline fallback
- ✅ **Passkey Auth** — WebAuthn credentials, no passwords
- ✅ **PWA** — Install as native app on Android/iOS

## Architecture

**Modular Monolith with CQRS** (DDD, Clean Architecture):
```
Angular PWA + RxDB (device)
    ↕ HTTPS/REST + SignalR
ASP.NET Core API + MediatR
    ↓
Domain Layer (entities, events, aggregates)
    ↓
SQL Server 2022
```

6 Bounded Contexts: Agroquímicos, Aplicaciones, Cosecha, Costos, Identidad, Sincronización.

[Full architecture →](docs/architecture/README.md)

## Project Structure

```
PitaCost/
├── docs/
│   ├── architecture/        ← System design, API contract, offline sync flow
│   └── database/            ← Schema (21 tables), indexes, stored procedures
├── src/
│   ├── backend/             ← .NET 9 solution: Domain, Application, Infrastructure, API
│   └── frontend/            ← Angular 18 PWA app
└── PROJECT_CONTEXT.md       ← Full project context & conventions
```

## Setup

### Prerequisites
- **.NET 9 SDK** (`dotnet --version`)
- **Node.js 20+** (`node --version`)
- **SQL Server 2022+** (or LocalDB)
- **Angular CLI 18+** (`npm install -g @angular/cli`)

### Backend
```bash
cd src/backend
dotnet restore
dotnet ef database update --project PitaSmart.Infrastructure
dotnet run --project PitaSmart.Api
```
Backend runs on `https://localhost:5001`.

### Frontend
```bash
cd src/frontend
npm install
ng serve
```
Frontend runs on `http://localhost:4200`.

### Database
SQL Server scripts in `docs/database/`:
1. `schema.sql` — Creates 21 tables (Insumo, AplicacionQuimico, Cosecha, etc.)
2. `seed-data.sql` — Adds test data (farmers, crops, chemicals)

Load via **SQL Server Management Studio** or:
```bash
sqlcmd -S (localdb)\mssqllocaldb -i docs/database/schema.sql
sqlcmd -S (localdb)\mssqllocaldb -i docs/database/seed-data.sql
```

## Running the App

1. **Start backend** — `cd src/backend && dotnet run --project PitaSmart.Api`
2. **Start frontend** — `cd src/frontend && ng serve`
3. **Open browser** — `http://localhost:4200`
4. **Login** — Use test credentials from `seed-data.sql` or register with Passkey

## API Reference

Base: `https://localhost:5001/api/v1`

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/aplicaciones` | Register chemical application |
| POST | `/cosechas` | Register harvest (validated for carencia) |
| GET | `/lotes/{id}/rentabilidad` | Profitability dashboard |
| POST | `/sync/push` | Offline sync batch |
| POST | `/auth/challenge` | Passkey auth start |
| POST | `/auth/verify` | Passkey verification → JWT |

[Full API spec →](docs/architecture/api-contract.md)

## Testing

### Manual Testing
- App works **offline**: disable network, register data, reconnect to sync
- **Carencia validation**: try registering harvest before safety period ends (should fail)
- **Profitability**: check dashboard updates as costs/income change

### Automated Tests
_Tests pending (unit: Domain, integration: API)_

## Documentation

- **[Architecture](docs/architecture/)** — System design, bounded contexts, patterns
- **[Database](docs/database/)** — Schema, indexes, ERD, decisions
- **[Project Context](src/PROJECT_CONTEXT.md)** — Conventions, tech decisions, roadmap
- **[API Contract](docs/architecture/api-contract.md)** — Endpoints, request/response, sync flow

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Follow conventions in [PROJECT_CONTEXT.md](src/PROJECT_CONTEXT.md)
4. Commit with clear messages
5. Push to branch: `git push origin feature/your-feature`
6. Open PR against `main`

## Roadmap (Post-MVP)

- [ ] jsreport PDF generation (official Agrocalidad reports)
- [ ] Automated tests (Domain + Integration)
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Device settings UI (manage registered Passkeys)
- [ ] Feature: Plots CRUD
- [ ] Feature: Chemical catalog browser
- [ ] SignalR integration (live market prices)

## License

[Pending — add LICENSE file]

## Contact

Questions? See [PROJECT_CONTEXT.md](src/PROJECT_CONTEXT.md) for full context.

<!-- AUTOREADME:END -->
