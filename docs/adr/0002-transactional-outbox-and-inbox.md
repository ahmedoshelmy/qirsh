# 2. Transactional outbox and idempotent consumers for money events

Date: 2026-11-20 · Status: accepted

## Context

Every service both changes its database and announces the change (`SmsParsed`, `TransactionRecorded`,
`BudgetThresholdCrossed`). Done naively (`SaveChanges()` then `Publish()`), a crash or broker outage between the
two calls either loses the event (the ledger never sees a payment) or, with a retry around both, publishes it
twice (a payment counted twice against a budget). For money that is not acceptable.

## Decision

- Use the **MassTransit EF Core transactional outbox** in every service. Publishing writes an `OutboxMessage` row
  through the same `DbContext`, so the business rows and the outgoing events commit in **one Postgres
  transaction**. A delivery service forwards committed rows to the broker and deletes them.
- Every consumer endpoint runs inside the **inbox** (`InboxState` keyed by message id + consumer): a redelivered
  message is recognised and skipped, and anything the consumer publishes goes through the outbox again.
- On top of message-id idempotency, each step also has a **natural key**:
  - Ingestion: unique `raw_messages.hash` (SHA-256 of the normalised SMS) – pasting the same SMS twice records it once.
  - Ledger: unique `(account_id, sms_hash)` on transactions.
  - Budgets: `applied_transactions(transaction_id)` – a transaction counts against a budget at most once, even if
    it arrives through two paths (event and gRPC backfill, see ADR 3).
- Budgets uses Postgres `xmin` as an optimistic concurrency token; a conflicting update fails and the consumer
  retry re-reads the budget.

## Consequences

- At-least-once delivery from the broker, exactly-once effect in the database.
- Events are delayed by the outbox polling interval (`Messaging:OutboxQueryDelay`, 1 s by default).
- MassTransit caches its row-lock SQL per entity type in a static map. With one schema per service and several
  services in one process (the integration tests), the first schema would win and the others would poll the wrong
  table. `UsePostgres(enableSchemaCaching: false)` resolves the SQL per `DbContext`. Found by the Testcontainers test.
