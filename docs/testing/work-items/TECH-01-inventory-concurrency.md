# TECH-01 — Enforce inventory concurrency invariants

| Field | Value |
|---|---|
| Type | Engineering/testing |
| Status | Accepted with residual |
| Owner | Inventory Engineering |
| Priority | Critical |
| Related | GAP-001, `ESHOP-INVENTORY-002` |

## Outcome

Prevent concurrent reservation attempts from overselling the last unit, partially reserving a losing multi-line order or leaving state behind after retry exhaustion.

## Problem

Sequential inventory tests did not prove behavior when independent requests or consumers raced on the same PostgreSQL rows. Optimistic concurrency existed, but its business invariant, retry boundary and durable side effects required deterministic evidence.

## Scope

- Real PostgreSQL concurrency using the `xmin` token.
- Deterministically synchronized first-wave writes.
- Last-unit competition, multi-line atomicity and bounded retry exhaustion.
- Broker-delivered two-consumer variant with downstream and DLQ assertions.

## Acceptance criteria

1. Two orders competing for the last unit produce exactly one reservation and one failure; stock never oversells.
2. A losing multi-line order reserves neither the constrained line nor any unconstrained companion line.
3. Successful and failed orders each produce exactly one appropriate result event and one inbox record.
4. Retry exhaustion raises a contextual terminal error and leaves inventory, inbox and outbox unchanged.
5. The broker variant uses two service hosts/consumers over one database and leaves all workflow and dead-letter queues empty after bounded stabilization.
6. Tests use deterministic barriers or interceptors rather than timing sleeps to create the race.
7. The variants remain bound to `ESHOP-INVENTORY-002` and run in governed deeper tiers.

## Oracle

For every contending order, the final stock equation and per-order terminal result must agree. No loser may own a partial reservation, duplicate message or dead-lettered retryable delivery.

## Result and residual

Accepted through CI #35/TestRail R46 and the first governed Nightly R49 and Release R50. Direct project evidence passed 17/17 and the three original variants passed 15/15 across five fresh runs. Longitudinal scheduled history remains immature.

## Source records

- [`../evidence-baseline.md`](../evidence-baseline.md#tech-01-evidence)
- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md)
- [`../traceability-matrix.md`](../traceability-matrix.md)

