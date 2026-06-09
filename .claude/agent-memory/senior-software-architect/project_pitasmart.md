---
name: PitaSmart Architecture Decisions
description: Core architectural decisions for PitaSmart SaaS - offline-first agriculture platform for Ecuador
type: project
---

PitaSmart is an offline-first SaaS for Ecuadorian farmers to track chemical applications, comply with Agrocalidad regulations (Periodo de Carencia), manage costs, and view profitability.

**Architecture**: Modular Monolith with CQRS (MediatR), Clean Architecture. Microservices and Serverless were evaluated and rejected for MVP.

**Why:** Small team, offline-first requirements favor transactional consistency and simple deployment. CQRS without Event Sourcing provides read/write separation without excessive complexity.

**How to apply:** All future design decisions should respect the Modular Monolith boundary. Bounded Contexts (Agroquimicos, Aplicaciones, Cosecha, Costos, Identidad, Sincronizacion) are internal modules with clear interfaces, designed to be extractable to services later if needed.

**Key tech stack**: .NET 9 backend, Angular 18+ PWA with RxDB, SQL Server 2022 with RowVersion, Passkeys (WebAuthn) + JWT fallback, SignalR for realtime prices, jsreport for PDF generation.

**Critical business rule**: Periodo de Carencia blocks harvests if chemical application waiting period hasn't elapsed. Validated both client-side (offline) and server-side (authoritative).

**Sync strategy**: Last-Write-Wins with RowVersion for non-critical entities; manual merge for regulated entities (AplicacionQuimico, Cosecha). Idempotency via operacionId UUID.

Architecture docs delivered on 2026-03-24 at docs/architecture/.
