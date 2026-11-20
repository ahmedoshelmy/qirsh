# 3. gRPC for cross-service queries, events for facts

Date: 2026-11-20 · Status: accepted

## Context

Budgets needs two kinds of information from the Ledger:

1. Every new spending fact, as it happens, to update `spent` and raise alerts.
2. When a budget is created mid-month, how much was **already** spent in that category this month.

Events fit (1). For (2), the Budgets service could keep its own copy of every transaction (a full projection) or
replay the event history, but it only needs one number, once, at creation time.

## Decision

- Facts flow as **events** over the broker (`TransactionRecorded`, `TransactionRecategorised`).
- The one-off question is a **synchronous gRPC query**, `LedgerQuery.GetMonthSpend(category, month, currency)`,
  owned by the Ledger and defined in `src/Qirsh.Contracts/protos/ledger_query.proto`.
- The call has a 3-second **deadline**. If the Ledger does not answer, the budget is still created with
  `spent = 0` and the response says `backfilled: false`; budget creation never fails because another service is down.
- The reply includes the **ids of the transactions it counted**. Budgets stores them in `applied_transactions`, so if
  one of those transactions' `TransactionRecorded` events is still in flight it is ignored when it arrives. This
  closes the race between "query the past" and "consume the future".

## Consequences

- Budgets does not need its own copy of the ledger; the Ledger stays the single source of truth for spend.
- A temporal coupling exists only at budget creation, and it degrades gracefully.
- gRPC runs on a separate HTTP/2-only port (Ledger 5202, Ingestion 5102) because Kestrel cannot serve h2c and
  HTTP/1.1 on the same plaintext port.
