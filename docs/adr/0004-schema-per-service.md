# 4. One Postgres, one schema per service

Date: 2026-11-20 · Status: accepted

## Context

Microservices should own their data: no service reads another's tables. Running one Postgres server per service on
a laptop (or paying for three in Azure) is wasteful for a self-hosted personal app.

## Decision

- One Postgres database (`qirsh`), one **schema per service**: `ingestion`, `ledger`, `budgets`.
- Each `DbContext` sets `HasDefaultSchema(...)` and keeps its **own migrations history table** in its schema
  (`<schema>.__ef_migrations`), so each service migrates independently and can be moved to its own server later by
  changing only a connection string.
- The MassTransit outbox and inbox tables live in each service's schema too; a service's events commit with its own data.
- Cross-service data access happens only through messages or the published gRPC contract (ADR 3). An architecture
  test checks that no bounded context references another's projects.

## Consequences

- One container in compose, one Postgres Flexible Server in Azure.
- Ownership is a convention enforced by code review and tests rather than by separate database users; per-schema
  roles (`GRANT USAGE ON SCHEMA ledger TO ledger_svc`) are a straightforward hardening step.
