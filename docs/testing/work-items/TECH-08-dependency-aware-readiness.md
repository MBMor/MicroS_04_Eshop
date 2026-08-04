# TECH-08 — Implement dependency-aware readiness

| Field | Value |
|---|---|
| Type | Engineering/platform |
| Status | Accepted |
| Owner | Platform Engineering |
| Priority | High |
| Related | QA-05, GAP-004, `ESHOP-RESILIENCE-002` |

## Outcome

Implement consistent liveness and dependency-aware readiness across all seven HTTP processes and prove material dependency outage and recovery behavior.

## Implementation scope

- Shared anonymous `/live`, `/ready` and compatibility `/health` registration.
- Bounded non-destructive PostgreSQL checks for Catalog, Inventory, Orders, Payments and Notifications.
- Bounded Redis readiness check for Basket.
- No downstream readiness dependency for Gateway and no RabbitMQ readiness dependency for outbox-backed services.
- E2E startup waits on `/ready`.

## Acceptance criteria

1. Every HTTP process exposes the three endpoints with QA-05 semantics.
2. Response bodies contain only `Healthy` or `Unhealthy` and no secret or connection detail.
3. Pausing real Catalog PostgreSQL keeps `/live=200`, makes `/ready` and `/health=503`, then recovers to 200 without service restart.
4. The same outage/recovery sequence is proven for Basket Redis.
5. Polling and probes are bounded; `finally` cleanup unpauses dependencies.
6. Gateway route governance includes the new local endpoints without authorization/rate-limit drift.
7. Both material selectors bind to existing C80/`ESHOP-RESILIENCE-002`, run on Main and Release and create no case.
8. Main publication uses the accepted evolved cardinality; full topology remains GAP-013 and `GATE-OPS-001` remains Future.

## Oracle

An owned dependency outage changes readiness but not process liveness. Restoring the same dependency must restore readiness without restarting the service, within a bounded observation window.

## Result

Accepted as `36f6c5d` through Main CI #66/R92 (24/24) and Release #4/R95 (9/9), with C80 passing in both. GAP-004 is closed.

## Source records

- [`QA-05-readiness-oracle-workshop.md`](QA-05-readiness-oracle-workshop.md)
- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#qa-05--tech-08-approved-readiness-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#qa-05--tech-08--gap-004-accepted-evidence)

