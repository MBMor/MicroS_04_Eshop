# Executable Test Evidence Baseline

> **QA work item:** QA-01  
> **Version:** 1.0  
> **As of:** 2026-07-28 (Europe/Prague)  
> **Repository baseline:** `main` / `a0c46a0ab74dd943ce055c578b2832757891d2ab`  
> **Working-tree scope:** pending TECH-01 inventory-concurrency changes

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

After TECH-01, the repository contains:

| Metric | Count | Definition |
|---|---:|---|
| Logical tests | 180 | one xUnit method, Vitest `it`, or Playwright `test`; a Theory counts once |
| Executable cases | 184 | logical tests plus four additional Theory rows |
| xUnit logical / executable | 167 / 171 | 163 Facts and four two-row Theories |
| Frontend Vitest | 10 | executable tests |
| Playwright | 3 | executable scenarios |
| TestRail source selectors | 180 | unique selectors in `automation-id-map.json` |
| TestRail binding edges | 190 | selector-to-TestIntent mappings; ten are deliberate multi-intent overlaps |

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

## Current interpretation

- QA-01 baseline refresh is complete for counts, provenance and CI/TestRail acceptance.
- TECH-01 implementation is locally verified and is pending shared CI evidence.
- TECH-02 remains a proposed oracle in [`ADR 0002`](../architecture/0002-checkout-command-idempotency.md); no idempotency coverage may be reported until the oracle is approved and implemented.
- Formal Nightly and Release tiers remain future work; all checked-in tests are still scheduled by the current PR/main workflow.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 1.0 | 2026-07-28 | Created QA-01 executable evidence baseline and recorded TECH-01 local proof separately from CI #28. | Pending review |
