# Executable Test Evidence Baseline

> **QA work item:** QA-01  
> **Version:** 1.3
> **As of:** 2026-07-28 (Europe/Prague)  
> **Repository baseline:** `main` / `36531190b45cf2591617b92e21dd0b207b84665c`
> **Working-tree scope:** QA-02 cross-service duplicate-checkout proof, stable replay `Location`, automation-map and evidence-document updates

This record separates shared CI/TestRail evidence from the additional local repeat evidence. It does not activate or pass any future quality gate.

## Committed CI and TestRail evidence

GitHub Actions run [`30381746424`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30381746424) (`CI #31`) on commit `03518fe52c5d8105ee55628a868a70dd20ba14fc` completed successfully on 2026-07-28. Backend, Frontend, Container images, Checkout E2E and Publish TestRail results all concluded `success`. TestRail received four closed runs, all 100% Passed:

| TestRail run | Area | TestIntent results | Result |
|---|---|---:|---|
| `R29` | Backend Unit | 12 | Passed |
| `R30` | Backend Integration | 28 | Passed |
| `R31` | Frontend Unit | 3 | Passed |
| `R32` | Checkout E2E | 4 | Passed |

The 45-case suite did not grow. The automation map contains 31 automated TestIntents; deliberate overlap between execution areas produced 47 aggregate result rows across the four runs. Planned/manual cases remained in the governed suite without synthetic results. `ESHOP-ORDER-002` passed in both `R30` and `R31`.

Evidence validity is **Valid** for the exact commit, environment and variants exercised by CI #31. The earlier `CI #28` / `R17`–`R20` record remains the historical acceptance of the TestRail publication mechanism.

## Current source baseline

The QA-02 working tree contains:

| Metric | Count | Definition |
|---|---:|---|
| Logical tests | 192 | one xUnit method, Vitest `it`, or Playwright `test`; a Theory counts once |
| Executable cases | 197 | logical tests plus five additional Theory rows |
| xUnit logical / executable | 176 / 181 | 171 Facts and five two-row Theories |
| Frontend Vitest | 13 | executable tests |
| Playwright | 3 | executable scenarios |
| TestRail automated TestIntents | 31 | aggregate IDs in `automation-id-map.json` |
| TestRail source selectors | 192 | unique selectors in `automation-id-map.json` |
| TestRail binding edges | 209 | selector-to-TestIntent mappings; seventeen are deliberate multi-intent overlaps |

Every logical test has exactly one unique source selector. Multiple TestIntent bindings are allowed and explain the difference between selectors and edges.

## TECH-01 evidence

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

The committed variants passed in the Backend job of CI #31 and aggregate `ESHOP-INVENTORY-002` passed in TestRail `R30`. The local 17/17 project run and five-run repeat remain supporting flake-smoke evidence. Scheduled repeat history and the broker-delivery/no-DLQ path remain open.

## TECH-02 evidence

The approved checkout-command oracle is implemented in Orders Service, its PostgreSQL migration, the React checkout client and the TestRail automation map. On 2026-07-28:

- the complete `OrdersService.IntegrationTests` project passed **17/17 executable cases**, with zero failures and zero skips, against a disposable PostgreSQL 18 Testcontainer;
- the complete frontend Vitest suite passed **13/13**;
- the direct concurrent-identical-request test then passed **5/5 fresh Testcontainer runs**, with zero failures and zero skips;
- the TestRail transformer tests passed **7/7**, and result preparation resolved all selectors without an unknown binding;
- TestRail case `C60` was synchronized to `Automated`, `Implemented`, `Approved`, native `Is Automated = Yes` and `Eshop.TestIntents.ESHOP-ORDER-002`.

JUnit provenance: `artifacts/test-results/orders-tech02/orders-tech02.junit.xml` and `artifacts/test-results/frontend/frontend-unit-tests.junit.xml` (generated evidence, intentionally not source artifacts).

The ten new logical tests directly prove header propagation, key lifecycle, invalid input, replay without a basket reload, changed-request conflict without side effects, concurrent uniqueness, customer scoping, current-basket use under a new key and replay after a failed basket clear. The variants passed in CI #31; aggregate `ESHOP-ORDER-002` passed in TestRail backend run `R30` and frontend run `R31`. The local 17/17 Orders run, 13/13 frontend run and deterministic 5/5 concurrency repeat remain supporting evidence.

## QA-02 local cross-service evidence

QA-02 adds two serialized messaging integration tests against real PostgreSQL and RabbitMQ Testcontainers with real Orders, Inventory, Payments and Notifications hosts:

| Test | Direct oracle |
|---|---|
| `DuplicateCheckoutReplayCreatesOneCompleteWorkflow` | first request is `201`, replay is `200` with the same order and absolute `Location`; one order/idempotency record, one basket clear and one complete downstream workflow |
| `ConcurrentDuplicateCheckoutCreatesOneCompleteWorkflow` | both requests read the same basket before racing; exactly one creator and one replay; the same durable and downstream cardinality oracle applies |

Both variants assert exactly one order and idempotency record, one inventory reservation, one authorized payment, four expected notifications, exact Orders/Inventory/Payments outbox counts, exact per-service inbox counts, and empty workflow queues and DLQs. The work also found and fixed a contract mismatch where the first response used an absolute `Location` but replay used a relative URL; the Orders regression now requires exact equality.

Local execution on 2026-07-28 produced:

| Scope | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Orders and Messaging builds | 2 | 0 | n/a |
| stable replay `Location` targeted Orders regression | 1 | 0 | 0 |
| QA-02 targeted cross-service variants | 2 | 0 | 0 |
| complete `Eshop.Messaging.IntegrationTests` | 12 | 0 | 0 |
| complete `OrdersService.IntegrationTests` | 17 | 0 | 0 |
| concurrent QA-02 variant across five independent runs | 5 | 0 | 0 |
| TestRail transformation unit tests | 7 | 0 | 0 |

The runtime map now has 192 selectors and 209 edges. Both QA-02 selectors bind to `ESHOP-ORDER-002` and `ESHOP-E2E-001`. These results are local supporting evidence until the exact working-tree commit passes shared CI and publishes to TestRail; scheduled Nightly history also remains pending.

## Current interpretation

- QA-01 baseline refresh is complete for counts, provenance and CI/TestRail acceptance.
- TECH-01 implementation and its named direct variants have Passed/Valid shared CI evidence; scheduled repeat history and broker-delivery/no-DLQ evidence remain pending.
- TECH-02 has Passed/Valid shared CI evidence for the named API/frontend variants. QA-02 locally closes the downstream one-workflow implementation gap; shared CI/TestRail publication and scheduled history remain pending.
- Formal Nightly and Release tiers remain future work; all checked-in tests are still scheduled by the current PR/main workflow.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 1.3 | 2026-07-28 | Recorded QA-02 local cross-service duplicate-checkout evidence, five-run concurrency smoke, stable replay Location fix and 192/197 source baseline. | Pending review |
| 1.2 | 2026-07-28 | Promoted TECH-01/TECH-02 named variants to shared evidence after CI #31 and TestRail R29–R32 passed on commit `03518fe`. | Pending review |
| 1.1 | 2026-07-28 | Recorded TECH-02 approval, 190/195 reconciled source counts, local Orders/frontend evidence, five concurrency repeats and TestRail C60 synchronization. | Pending review |
| 1.0 | 2026-07-28 | Created QA-01 executable evidence baseline and recorded TECH-01 local proof separately from CI #28. | Pending review |
