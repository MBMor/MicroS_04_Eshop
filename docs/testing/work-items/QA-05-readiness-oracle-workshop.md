# QA-05 — Approve dependency-aware readiness oracle

| Field | Value |
|---|---|
| Type | QA architecture / oracle workshop |
| Status | Accepted |
| Owner | QA / Platform |
| Priority | High |
| Related | TECH-08, GAP-004 |

## Outcome

Define when orchestration may send traffic to a service without confusing a dependency outage with a dead process.

## Approved decisions

- `/live` reports process viability only.
- `/ready` evaluates owned mandatory synchronous dependencies.
- Transitional `/health` is an alias of `/ready`.
- Catalog, Inventory, Orders, Payments and Notifications own PostgreSQL readiness.
- Basket owns Redis readiness.
- Gateway has no mandatory runtime downstream readiness dependency.
- RabbitMQ is excluded because outbox-backed services are expected to tolerate a temporary broker outage.
- Downstream HTTP services are excluded to avoid cascading service removal.

## Acceptance criteria

1. All seven HTTP processes expose anonymous `/live`, `/ready` and `/health`.
2. Probes are bounded, non-destructive and reveal no connection data or secrets.
3. During Catalog PostgreSQL or Basket Redis outage, `/live` remains 200 while `/ready` and `/health` return 503.
4. Dependency recovery returns readiness to 200 without restarting the HTTP service.
5. Polling is condition-based and bounded; cleanup restores the dependency even after failure.
6. Material variants aggregate to existing `ESHOP-RESILIENCE-002` and run on Main and Release.
7. Full Compose/delayed-dependency topology remains GAP-013; `GATE-OPS-001` remains Future and unevaluated.

## Oracle

An owned synchronous dependency determines readiness but not liveness. A temporary non-owned or asynchronously buffered dependency outage must not automatically remove the process from service.

## Result

Oracle approved and implemented by TECH-08. Accepted through Main CI #66/R92 and Release #4/R95, including C80 `ESHOP-RESILIENCE-002`.

## Source records

- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#qa-05--tech-08-approved-readiness-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#qa-05--tech-08--gap-004-accepted-evidence)
- [`TECH-08-dependency-aware-readiness.md`](TECH-08-dependency-aware-readiness.md)

