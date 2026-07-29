# Executable Test Evidence Baseline

> **QA work item:** QA-01  
> **Version:** 2.4
> **As of:** 2026-07-29 (Europe/Prague)
> **Repository baseline:** `main` / `06b8895`
> **Working-tree scope:** local TECH-05/GAP-026 gateway authorization evidence, pending shared acceptance

This record separates shared CI/TestRail evidence from the additional local repeat evidence. It does not activate or pass any future quality gate.

## GAP-001 committed CI and TestRail evidence

GitHub Actions run [`30429176555`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30429176555) (`CI #35`) on commit `1da2ccb` completed successfully on 2026-07-29. Quality policy, Backend, Frontend, Container images, Checkout E2E and Publish TestRail results all concluded `success`. TestRail received four closed runs, all 100% Passed:

| TestRail run | Area | TestIntent results | Result |
|---|---|---:|---|
| `R45` | Backend Unit | 12 | Passed |
| `R46` | Backend Integration | 28 | Passed |
| `R47` | Frontend Unit | 3 | Passed |
| `R48` | Checkout E2E | 4 | Passed |

The 45-case suite did not grow. The automation map contains 31 automated TestIntents, 193 unique selectors and 211 binding edges; deliberate overlap between execution areas produced 47 aggregate result rows across the four runs. Planned/manual cases remained in the governed suite without synthetic results. The GAP-001 broker selector resolved without creating cases; both `ESHOP-INVENTORY-002` and `ESHOP-DATA-002` passed again in `R46`.

Evidence validity is **Valid** for the exact commit, environment and variants exercised by CI #35. `CI #34` remains the initial shared GAP-001 broker baseline, `CI #33` the initial shared QA-02 baseline, and `CI #28` / `R17`–`R20` the acceptance of the publication mechanism.

## Current source baseline

The local TECH-05 source baseline contains the counts below. Accepted shared publication remains CI #46/R71–R74 until the change reaches Main.

| Metric | Count | Definition |
|---|---:|---|
| Logical tests | 194 | one xUnit method, Vitest `it`, or Playwright `test`; a Theory counts once |
| Executable cases | 241 | logical tests plus five two-row Theory increments and 42 additional gateway-matrix rows |
| xUnit logical / executable | 178 / 225 | 172 Facts, five two-row Theories and one 43-row Theory |
| Frontend Vitest | 13 | executable tests |
| Playwright | 3 | executable scenarios |
| TestRail automated TestIntents | 31 | aggregate IDs in `automation-id-map.json` |
| TestRail source selectors | 194 | unique selectors in `automation-id-map.json` |
| TestRail binding edges | 212 | selector-to-TestIntent mappings; eighteen are deliberate multi-intent overlaps |

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

The committed variants passed in the Backend job of CI #31 and aggregate `ESHOP-INVENTORY-002` passed in TestRail `R30`. The local 17/17 project run and five-run repeat remain supporting flake-smoke evidence. The working tree now adds the broker-delivery/no-DLQ variant described below; scheduled repeat history remains open.

## TECH-02 evidence

The approved checkout-command oracle is implemented in Orders Service, its PostgreSQL migration, the React checkout client and the TestRail automation map. On 2026-07-28:

- the complete `OrdersService.IntegrationTests` project passed **17/17 executable cases**, with zero failures and zero skips, against a disposable PostgreSQL 18 Testcontainer;
- the complete frontend Vitest suite passed **13/13**;
- the direct concurrent-identical-request test then passed **5/5 fresh Testcontainer runs**, with zero failures and zero skips;
- the TestRail transformer tests passed **7/7**, and result preparation resolved all selectors without an unknown binding;
- TestRail case `C60` was synchronized to `Automated`, `Implemented`, `Approved`, native `Is Automated = Yes` and `Eshop.TestIntents.ESHOP-ORDER-002`.

JUnit provenance: `artifacts/test-results/orders-tech02/orders-tech02.junit.xml` and `artifacts/test-results/frontend/frontend-unit-tests.junit.xml` (generated evidence, intentionally not source artifacts).

The ten new logical tests directly prove header propagation, key lifecycle, invalid input, replay without a basket reload, changed-request conflict without side effects, concurrent uniqueness, customer scoping, current-basket use under a new key and replay after a failed basket clear. The variants passed in CI #31; aggregate `ESHOP-ORDER-002` passed in TestRail backend run `R30` and frontend run `R31`. The local 17/17 Orders run, 13/13 frontend run and deterministic 5/5 concurrency repeat remain supporting evidence.

## QA-02 cross-service evidence

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

The runtime map has 192 selectors and 209 edges. Both QA-02 selectors bind to `ESHOP-ORDER-002` and `ESHOP-E2E-001`. Commit `a1fba95` passed CI #33 and both aggregates passed in TestRail R38. The local full-project and five-run results remain supporting determinism evidence; scheduled Nightly history remains pending.

## GAP-001 broker-delivery evidence

`InventoryConcurrencyMessagingTests.ConcurrentOrderCreatedDeliveriesForLastUnitDoNotOversellOrDeadLetter` runs two real Inventory service hosts and RabbitMQ consumers over one PostgreSQL database. A shared EF interceptor synchronizes the first reservation commits while two independent Orders workflows compete for the last unit.

The direct oracle requires exactly one Confirmed order and one `StockReservationFailed` order; stock `OnHand = 1`, `Reserved = 1`, `Available = 0`; two Inventory inbox rows and two published result events; exact Orders, Payments and Notifications inbox/outbox cardinality; and zero ready messages in every main and dead-letter queue after a stability delay. The losing consumer must retry after the optimistic concurrency conflict instead of overselling or dead-lettering the retryable message.

Local execution on 2026-07-28 produced:

| Scope | Passed | Failed | Skipped |
|---|---:|---:|---:|
| targeted broker-delivery variant across three independent Testcontainer runs | 3 | 0 | 0 |
| complete `Eshop.Messaging.IntegrationTests` | 13 | 0 | 0 |
| Messaging project build | 1 | 0 | n/a |
| TestRail transformation unit tests | 7 | 0 | 0 |

The runtime map has 193 selectors and 211 edges. The GAP-001 selector binds to both `ESHOP-DATA-002` and `ESHOP-INVENTORY-002`. Commit `1da2ccb` passed CI #35; both aggregates passed again in TestRail R46.

## QA-03 tier policy evidence

Commit `1da2ccb` introduced [`scripts/quality/test-tier-policy.json`](../../scripts/quality/test-tier-policy.json) as the authoritative classification contract. Every governed selector matches exactly one source/project and one primary tier; Release is an explicit overlap. Fail-closed validation reports `PR=77`, `Main=97`, `Nightly=19` and `Release=13` for all 193 selectors.

[`scripts/quality/test_tiers.py`](../../scripts/quality/test_tiers.py) validates mapping drift and produces exact .NET execution matrices. Local execution passed Nightly project counts `Inventory=3/3`, `Messaging=9/9`, `Orders=8/8`; Release-specific messaging overlap passed `3/3`. Because Release reuses the same Inventory and Orders selectors, all 14 executable rows in its current selection have passing local evidence. The Orders subset has eight executable rows because its seven logical selectors include one two-row Theory. The policy also locks publication cardinality: Nightly produces 11 TestIntent aggregates from 25 binding edges, while Release produces 6 aggregates from 21 edges.

The committed [`quality-tiers.yml`](../../.github/workflows/quality-tiers.yml) schedules Nightly daily and exposes Nightly/Release through `workflow_dispatch`. The first shared acceptance passed on 2026-07-29:

| Tier | GitHub Actions | TestRail | Aggregates | Result |
|---|---|---|---:|---|
| Nightly | [`30430788377`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30430788377) | `R49` | 11 | 11 Passed, 0 Failed/Skipped/Blocked |
| Release | [`30430855956`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30430855956) | `R50` | 6 | 6 Passed, 0 Failed/Skipped/Blocked |

Both TestRail runs are closed, named by governed tier and linked to their originating GitHub run. This accepts selection, execution, transformation and publication mechanics. It is the first shared tier observation, not longitudinal flake history and not a release decision or gate activation.

## GAP-022 PR/Main cutover evidence

Merge commit `a41aa71` makes event semantics explicit while retaining the Nightly/Release contract:

| Event profile | Logical selectors | Executable rows | TestRail publication |
|---|---:|---:|---|
| Pull request | 77 (`64` backend unit + `13` frontend) | 79 (`66` backend unit + `13` frontend) | none; fork/PR secrets are not consumed |
| Main push / workflow dispatch | 174 (`77` PR + `97` Main primary) | 178 (`66` backend unit + `96` backend integration + `13` frontend + `3` E2E) | four area runs |

PR run [`30433749355`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433749355) (`CI #37`) completed successfully in 1m 51s: Quality policy, Backend and Frontend passed; Container images, Checkout E2E and Publish TestRail results were skipped. This accepts the no-Docker/no-browser/no-secret PR path while retaining backend integration compilation.

Main run [`30433934594`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433934594) (`CI #38`) completed successfully on merge commit `a41aa71`; all six jobs passed, including Container images, Checkout E2E and TestRail publication. TestRail created four closed 100% Passed runs:

| TestRail run | Area | TestIntent results | Result |
|---|---|---:|---|
| `R55` | Backend Unit | 12 | Passed |
| `R56` | Backend Integration | 22 | Passed |
| `R57` | Frontend Unit | 3 | Passed |
| `R58` | Checkout E2E | 4 | Passed |

The three mixed integration projects use policy-generated positive filters: Inventory `14`, Messaging `4` and Orders `9`. Main is cumulative rather than exclusive, preserving fast-test coverage for direct pushes. The two shared event paths and locked TestRail cardinality are accepted; GAP-022 is complete. This records workflow evidence only and does not activate a quality gate.

## TECH-03 / GAP-020 evidence

The four existing `ESHOP-DATA-004` selectors were strengthened without renaming tests or changing the 193-selector/211-edge runtime contract:

| Project / scope | Passed | Failed | Skipped | Direct evidence |
|---|---:|---:|---:|---|
| Catalog targeted negative mutation | 1 | 0 | 0 | ValidationProblemDetails fields/error/trace/request IDs; product count unchanged |
| Orders targeted negative mutations | 3 | 0 | 0 | traceable errors; basket retained or not called; all Orders persistence tables empty |
| Catalog full integration regression | 10 | 0 | 0 | no regression across Catalog API behavior |
| Orders full integration regression | 17 | 0 | 0 | no regression across Orders auth, creation, idempotency, ownership and rejection behavior |

Orders controller-created errors now set the canonical HTTP-status type, request instance, `traceId` and `requestId`; explicitly typed idempotency errors retain their URN. The targeted runs used PostgreSQL Testcontainers and passed after a Release build. Policy validation remains `193` selectors with Main `174`, and `ESHOP-DATA-004` remains an existing four-edge Main aggregate.

PR run [`30437936730`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30437936730) (`CI #41`) completed successfully. Main run [`30438337628`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30438337628) (`CI #42`) then completed successfully on commit `c587eb9` and published four closed 100% Passed TestRail runs:

| TestRail run | Area | TestIntent results | Result |
|---|---|---:|---|
| `R63` | Backend Unit | 12 | Passed |
| `R64` | Backend Integration | 22 | Passed; includes `[Negative mutations]` / `ESHOP-DATA-004` |
| `R65` | Frontend Unit | 3 | Passed |
| `R66` | Checkout E2E | 4 | Passed |

The locked `12/22/3/4` cardinality and existing TestRail identity were preserved. This accepts TECH-03 and closes GAP-020 without changing a risk score, control state or quality-gate lifecycle.

TECH-03 did not claim the adjacent Catalog media-type finding; TECH-04 addresses it separately below.

## TECH-04 evidence

The pre-fix targeted Catalog run failed 0/1 because the observed response media type was `application/json` despite the previous `ObjectResult.ContentTypes` declaration. TECH-04 returns the same `ValidationProblemDetails` through an explicit `JsonResult` with `application/problem+json` and adds a fail-fast header assertion to the existing selector.

| Verification | Passed | Failed | Result |
|---|---:|---:|---|
| Targeted invalid Catalog create | 1 | 0 | exact ProblemDetails media type plus existing body/correlation/no-write oracle |
| Full Catalog integration regression | 10 | 0 | all Catalog API behavior retained |
| Release solution build | all projects | 0 | zero warnings and zero errors |
| Quality/TestRail Python controls | 17 + 7 | 0 | selector, tier, mapping and transformation contracts retained |

PR run [`30442564466`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30442564466) (`CI #45`) completed successfully. Main run [`30442756343`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30442756343) (`CI #46`) then completed successfully on commit `b298107` and published four closed 100% Passed TestRail runs:

| TestRail run | Area | TestIntent results | Result |
|---|---|---:|---|
| `R71` | Backend Unit | 12 | Passed |
| `R72` | Backend Integration | 22 | Passed; includes `[Negative mutations]` / `ESHOP-DATA-004` |
| `R73` | Frontend Unit | 3 | Passed |
| `R74` | Checkout E2E | 4 | Passed |

Selector count remains 193, mapping remains 211 edges, `ESHOP-DATA-004` remains a four-selector Main aggregate and the locked TestRail cardinality remains `12/22/3/4`. This accepts TECH-04 without changing TestRail identity, risk, control or gate state.

## Docs-only gate acceptance

PR run [`30444117840`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30444117840) (`CI #47`) and Main run [`30444165223`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30444165223) (`CI #48`) passed on the docs-only change ending at `06b8895`. Only Change scope and Quality policy executed. Backend, Frontend, Container images, Checkout E2E and TestRail publication were skipped, so TestRail stayed at 62 completed runs and R71–R74 remained the latest publication. This accepts the docs-only optimization without creating redundant execution evidence.

## TECH-05 local evidence

The authoritative gateway registry and integration matrix were verified locally on 2026-07-29:

| Verification | Passed | Failed | Result |
|---|---:|---:|---|
| Targeted gateway authorization matrix | 43 | 0 | all 13 proxy routes and 3 local endpoints; denial non-forwarding and successful exact forwarding |
| Full `ApiGateway.IntegrationTests` regression | 65 | 0 | 0 skipped; all existing gateway behavior retained |
| Quality policy unit tests | 24 | 0 | route registry, tier, mapping and report-cardinality drift controls |
| TestRail transformation unit tests | 7 | 0 | aggregate/publication contract retained |

[`gateway_routes.py`](../../scripts/quality/gateway_routes.py) reconciles [`gateway-route-policy.json`](../../scripts/quality/gateway-route-policy.json) against the production gateway configuration and reports `16` routes (`13` proxy, `3` local). The new Main selector binds to existing `ESHOP-GW-001`, so the local contract becomes 194 selectors/212 edges, Main primary 98 and cumulative Main 175. The four Main report aggregates remain locked at `12/22/3/4`; no TestRail case or gate state changes. This evidence is local until PR and Main CI pass and Main republishes the aggregate.

## Current interpretation

- QA-01 baseline refresh is complete for counts, provenance and CI/TestRail acceptance.
- TECH-01 and the broker-delivery/no-DLQ variant have Passed/Valid CI plus first shared Nightly/Release evidence; longitudinal scheduled history remains immature.
- TECH-02 and QA-02 have Passed/Valid CI plus first shared Nightly/Release evidence for the governed variants; longitudinal scheduled history remains immature.
- QA-03 Nightly/Release selection and publication are accepted. PR CI #37 and Main CI #38/R55–R58 complete GAP-022 and its W1 workflow substrate; no gate is activated by this evidence record.
- TECH-03 supplies accepted direct atomic-rejection evidence for the existing `ESHOP-DATA-004` aggregate; CI #41/#42 and TestRail R64 close GAP-020.
- TECH-04 fixes and locks the adjacent Catalog ProblemDetails media type; CI #45/#46 and TestRail R72 accept it without reopening GAP-020.
- The docs-only gate is accepted by CI #47/#48 and correctly produced no TestRail runs.
- TECH-05 supplies local direct gateway route/policy/non-forwarding evidence; shared Main/TestRail acceptance is still pending and direct Catalog network isolation remains GAP-003.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 2.4 | 2026-07-29 | Recorded docs-only CI #47/#48 acceptance and local TECH-05 evidence: 16 endpoints, 43 matrix rows, 65/65 gateway regression and 194/212 runtime mapping. | Pending review |
| 2.3 | 2026-07-29 | Accepted TECH-04 through CI #45/#46 and TestRail R71–R74 with unchanged TestRail identity and cardinality. | Pending review |
| 2.2 | 2026-07-29 | Added local TECH-04 pre-fix failure and post-fix Catalog/build/governance evidence with unchanged TestRail identity. | Pending review |
| 2.1 | 2026-07-29 | Accepted TECH-03 through CI #41/#42 and TestRail R63–R66; closed GAP-020 without changing risk or gate state. | Pending review |
| 2.0 | 2026-07-29 | Recorded local TECH-03/GAP-020 ProblemDetails, no-write and basket-retention evidence with unchanged TestRail identity/cardinality. | Pending review |
| 1.9 | 2026-07-29 | Accepted PR CI #37 and Main CI #38/TestRail R55–R58 and closed GAP-022 without activating a gate. | Pending review |
| 1.8 | 2026-07-29 | Implemented and locally evidenced the governed PR=77/Main=174 cumulative cutover with locked TestRail report counts. | Pending review |
| 1.7 | 2026-07-29 | Promoted commit `1da2ccb` through CI #35/R45–R48 and accepted QA-03 Nightly R49 plus Release R50 shared publication. | Pending review |
| 1.6 | 2026-07-29 | Promoted GAP-001 through CI #34/TestRail R42 and recorded the local QA-03 77/97/19/13 tier contract and workflow. | Pending review |
| 1.5 | 2026-07-28 | Added local GAP-001 two-consumer broker-delivery/no-DLQ proof, three-run stability evidence and the 193/198 source baseline. | Pending review |
| 1.4 | 2026-07-28 | Promoted QA-02 to shared evidence after CI #33 and TestRail R37–R40 passed on commit `a1fba95`. | Pending review |
| 1.3 | 2026-07-28 | Recorded QA-02 local cross-service duplicate-checkout evidence, five-run concurrency smoke, stable replay Location fix and 192/197 source baseline. | Pending review |
| 1.2 | 2026-07-28 | Promoted TECH-01/TECH-02 named variants to shared evidence after CI #31 and TestRail R29–R32 passed on commit `03518fe`. | Pending review |
| 1.1 | 2026-07-28 | Recorded TECH-02 approval, 190/195 reconciled source counts, local Orders/frontend evidence, five concurrency repeats and TestRail C60 synchronization. | Pending review |
| 1.0 | 2026-07-28 | Created QA-01 executable evidence baseline and recorded TECH-01 local proof separately from CI #28. | Pending review |
