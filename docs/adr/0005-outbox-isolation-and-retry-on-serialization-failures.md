# 5. Read-committed outbox, atomic hot-row updates, retry on lost races

Date: 2026-11-21 · Status: accepted

## Context

The [observability lab](https://github.com/ahmedoshelmy/observability-lab) load-tested SMS intake
(`docs/load-testing.md` there). HTTP stayed fast and error-free, but after a cold start a 2 → 40 req/s ramp left
900–4,400 rows in the outbox, draining at about one row per second, and 13 SMS ended in `sms-parsed_error`:

- Logs: `40001: could not serialize access due to concurrent update`, 74–210 per run in ingestion and 450–2,626 in
  the ledger, followed by MassTransit back-off (`Retrying 00:00:25`, `00:00:35`).
- Metric: send-to-consume delivery p95 went from 122 ms (steady 10 req/s) to over 10 s.
- A trace showed the ledger consumer failing with 40001 and succeeding on retry 180 ms later.

Root cause, in two parts:

1. **Isolation level.** MassTransit's EF Core outbox runs the delivery sweep and every consumer (inbox + outbox)
   transaction at `RepeatableRead` unless told otherwise, and we never told it. In Postgres a repeatable-read
   transaction that locks or updates a row changed after its snapshot fails with 40001 instead of waiting.
2. **A hot row in the ledger.** Every SMS that matches a merchant rule did `rule.MarkApplied()`, a tracked
   `TimesApplied++` saved by EF as `UPDATE merchant_rules SET "TimesApplied" = <value read earlier>`. The load test
   (like real life) has a handful of merchants, so dozens of concurrent consumers updated the same few rows: a
   serialization failure at repeatable read, and a silently **lost update** at read committed. Opening the same
   account from two consumers on a cold start also raced on the unique `(bank, last4)` index (23505).

The existing retry policy (100/500/2000 ms) gave up after three attempts, so the unlucky messages went to `_error`.

A Testcontainers test (`BurstIntakeTests`: 200 SMS posted concurrently, same three cards and eight merchants as the
lab) reproduced it on unfixed `main`: after 60 s only 196 of 200 reached the ledger, the other 4 were dead-lettered.

## Decision

- Set `IsolationLevel.ReadCommitted` on the EF Core outbox (`MessagingExtensions`). The outbox and inbox rows are
  locked with `SELECT … FOR UPDATE` (`SKIP LOCKED` for the sweep), which waits rather than fails at this level.
  Correctness of the business writes no longer relies on the isolation level:
  - unique indexes (SMS hash, `(account, sms hash)`, applied transaction id, `(bank, last4)`) reject duplicates;
  - Budgets already uses `xmin` optimistic concurrency, so a conflicting update fails and is retried;
  - the rule counter becomes one atomic statement, `ExecuteUpdate(... TimesApplied = TimesApplied + 1)`, run in the
    consumer's transaction just before `SaveChanges`, so the row lock on a popular rule is held only until commit.
- Treat a lost race as transient: serialization failure (40001), deadlock (40P01), unique violation (23505: the
  other consumer inserted the same key; the retry sees it and reports a duplicate) and EF's
  `DbUpdateConcurrencyException`. These get their own retry policy, 8 attempts with jittered exponential back-off
  from 20 ms to 2 s, nested inside the general 100/500/2000 ms policy, which ignores them so the two never multiply.
- **Not** partitioning the ledger consumer by account. The contended rows were merchant rules, not accounts, and
  with three accounts in the test a partitioner keyed on account would have capped the ledger at three concurrent
  messages without removing the conflict.

## Consequences

- `BurstIntakeTests` passes: all 200 SMS reach the ledger, no `_error` queue has a message, and the sum of rule hit
  counters is at least 200 (no lost increments).
- Re-measured with the lab's cold-start 2 → 40 req/s ramp, twice ("After the fix" in the lab's
  `docs/load-testing.md`): outbox backlog peak 8 and 3 (was 1,990), 0 `40001` log lines (was 1,668), nothing
  dead-lettered, every SMS in the ledger by the time k6 stopped (was 1,155 of 3,079 a minute later), send-to-consume
  p95 243 and 71 ms (was over 10 s). The second run's ledger retried 18 account-opening `23505` races, all recorded.
- Any new read-modify-write on a shared row must be atomic SQL or carry a concurrency token; read committed will not
  catch it. The architecture tests do not enforce this; code review must.
- A message that keeps losing races for 8 attempts (about 7 s) still goes to `_error`, so a real contention bug
  stays visible instead of retrying forever.
