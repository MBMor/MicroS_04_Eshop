# Executable Test Evidence Baseline

> **QA work item:** QA-01  
> **Version:** 1.1
> **As of:** 2026-07-28 (Europe/Prague)  
> **Repository baseline:** `main` / `a0c46a0ab74dd943ce055c578b2832757891d2ab`  
> **Working-tree scope:** pending TECH-01 inventory-concurrency and TECH-02 checkout-idempotency changes

This record separates committed CI evidence from local evidence produced by the pending change. It does not activate or pass any future quality gate.

## Committed CI and TestRail evidence

GitHub Actions run `30356073486` (`CI #28`) on commit `b0efb0c3d62b6044e59059f29687449778f572a0` completed successfully on 2026-07-28. TestRail received four closed runs, all 100% Passed:

| TestRail run | Area | TestIntent results | Result |
|---|---|---:|---|
| `R17` | Backend Unit | 12 | Passed |
| `R18` | Backend Integration | 27 | Passed |
| `R19` | Frontend Unit | 2 | Passed |
| `R20` | Checkout E2E | 4 | Passed |

The 45-case suite did not grow. The automation map contains 30 automated TestIntents; deliberate overlap between execution areas produced 45 aggregate result rows across the four runs. Planned/manual cases remained in the governed suite without synthetic results.

Evidence validity is **Valid** for the exact commit, environment and variants exercised by CI #28. It is not evidence for the later TECH-01 tests.

## Current source baseline

After TECH-01 and TECH-02, the repository contains:

| Metric | Count | Definition |
|---|---:|---|
| Logical tests | 190 | one xUnit method, Vitest `it`, or Playwright `test`; a Theory counts once |
| Executable cases | 195 | logical tests plus five additional Theory rows |
| xUnit logical / executable | 174 / 179 | 169 Facts and five two-row Theories |
| Frontend Vitest | 13 | executable tests |
| Playwright | 3 | executable scenarios |
| TestRail automated TestIntents | 31 | aggregate IDs in `automation-id-map.json` |
| TestRail source selectors | 190 | unique selectors in `automation-id-map.json` |
| TestRail binding edges | 205 | selector-to-TestIntent mappings; fifteen are deliberate multi-intent overlaps |

Every logical test has exactly one unique source selector. Multiple TestIntent bindings are allowed and explain the difference between selectors and edges.

## TECH-01 local evidence

The full `InventoryService.IntegrationTests` project was run in Debug against a disposable PostgreSQL 18 Testcontainer on 2026-07-28:

```text
Passed: 17
Failed: 0
Skipped: 0
Duration reported by test runner: 6 s
```

JUnit provenance: `artifacts/test-results/inventory-current/inventory-current.junit.xml` (generated evidence, intentionally not a source artifact).

The three TECH-01 tests were then repeated in five fresh test runs. All **15/15 repeated executions passed**, with zero failures and zero skips. This is a local flake smoke, not a substitute for scheduled CI/Nightly history.

The three added direct tests prove these named variants:

| Test | Direct oracle |
|---|---|
| `ConcurrentReservationsForLastUnitDoNotOversellAndRetryLoser` | two first-wave saves contend on real PostgreSQL `xmin`; exactly one reserve, one failure, no oversell, two inbox records and one result event per order |
| `ConcurrentMultiLineReservationsDoNotPartiallyReserveLosingOrder` | one winning two-line reservation; the losing order reserves neither constrained nor shared stock |
| `ReservationConcurrencyRetryExhaustionLeavesDatabaseUnchanged` | three deterministic conflicts; contextual terminal exception; inventory, inbox and outbox remain unchanged |

This is valid local first-attempt and five-run repeat evidence for the named variants. Promotion to shared evidence still requires a GitHub Actions run after commit; scheduled repeat history and the broker-delivery path remain open.

## TECH-02 local evidence

The approved checkout-command oracle is implemented in Orders Service, its PostgreSQL migration, the React checkout client and the TestRail automation map. On 2026-07-28:

- the complete `OrdersService.IntegrationTests` project passed **17/17 executable cases**, with zero failures and zero skips, against a disposable PostgreSQL 18 Testcontainer;
- the complete frontend Vitest suite passed **13/13**;
- the direct concurrent-identical-request test then passed **5/5 fresh Testcontainer runs**, with zero failures and zero skips;
- the TestRail transformer tests passed **7/7**, and result preparation resolved all selectors without an unknown binding;
- TestRail case `C60` was synchronized to `Automated`, `Implemented`, `Approved`, native `Is Automated = Yes` and `Eshop.TestIntents.ESHOP-ORDER-002`.

JUnit provenance: `artifacts/test-results/orders-tech02/orders-tech02.junit.xml` and `artifacts/test-results/frontend/frontend-unit-tests.junit.xml` (generated evidence, intentionally not source artifacts).

The ten new logical tests directly prove header propagation, key lifecycle, invalid input, replay without a basket reload, changed-request conflict without side effects, concurrent uniqueness, customer scoping, current-basket use under a new key and replay after a failed basket clear. This is Valid local evidence for those named variants. It does not yet prove a single downstream payment/inventory workflow under duplicate HTTP delivery, nor does it replace shared CI or scheduled Nightly history.

## Current interpretation

- QA-01 baseline refresh is complete for counts, provenance and CI/TestRail acceptance.
- TECH-01 implementation is locally verified and is pending shared CI evidence.
- TECH-02 is approved and locally verified for the named API/frontend variants; shared CI, scheduled repeat history and the downstream one-workflow assertion remain pending.
- Formal Nightly and Release tiers remain future work; all checked-in tests are still scheduled by the current PR/main workflow.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 1.1 | 2026-07-28 | Recorded TECH-02 approval, 190/195 reconciled source counts, local Orders/frontend evidence, five concurrency repeats and TestRail C60 synchronization. | Pending review |
| 1.0 | 2026-07-28 | Created QA-01 executable evidence baseline and recorded TECH-01 local proof separately from CI #28. | Pending review |
