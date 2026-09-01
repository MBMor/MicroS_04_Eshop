# TestRail CI integration

This repository uses TestRail's code-first JUnit flow with a deliberate aggregation layer.

The TestRail suite contains 45 high-level TestIntents, while the automated implementation contains many lower-level xUnit, Vitest and Playwright tests. Raw test cases are therefore retained as CI artifacts, but TestRail receives one synthetic JUnit result per automated TestIntent.

## Current checked-in contract

At source baseline `a8d344a`, the automation binding map contains 35 automated TestIntents.

The governed test-tier policy defines:

* PR ownership: 77 logical selectors
* Main ownership: 113 logical selectors
* cumulative Main execution: 190 logical selectors
* Nightly execution: 19 logical selectors
* Release overlap: 17 logical selectors
* Main aggregate publication: 32 TestIntents
* Release aggregate publication: 9 TestIntents

The current Main publication cardinality is:

```text
Backend Unit         12
Backend Integration  26
Frontend Unit         3
Checkout E2E          4
```

or, in compact form:

```text
12/26/3/4
```

The Release contract remains an explicit 17-selector overlap producing 9 aggregate TestIntents over 26 mapping edges.

## Historical accepted baseline

The accepted TECH-08 baseline contained 198 unique source selectors, 217 binding edges and 33 automated TestIntents.

Its Main publication contract was:

```text
12/24/3/4
```

Main run `30486673945` published CI #66/R92 at 24/24 Passed, and governed Release `30487730431` published Release #4/R95 at 9/9 Passed.

These TECH-08 values are retained as historical execution evidence. They are not the current checked-in publication contract.

## Identity contract

TRCLI matches the standard JUnit identity `classname.name`. Aggregated reports always use:

```text
classname = Eshop.TestIntents
name      = ESHOP-<AREA>-<NUMBER>
Automation ID = Eshop.TestIntents.ESHOP-<AREA>-<NUMBER>
```

The custom TestRail case field must be a String field named **Automation ID** with system name `automation_id`. It is optional and applies to all templates. Every case whose governed `Automation Status` is `Automated` must contain its aggregate ID; manual and planned cases remain empty. Custom `Automation Status` remains authoritative and native `Is Automated` stays synchronized (`Automated` = Yes, otherwise No).

Do not use TestRail case IDs, case titles, references, a JUnit `test_id` property or `--case-matcher property` for result matching. The repository has a single-suite project, so no suite ID is configured.

## Variant 1 aggregation

[`automation-id-map.json`](../../scripts/testrail/automation-id-map.json) is the runtime binding contract distilled from the external TestRail catalogue. Keys are stable canonical source selectors such as `OrderTests.CreateValidItemsCreatesPendingOrderAndInitialHistory`; values are represented by the containing TestIntent arrays. One raw test can support several TestIntents.

[`prepare_ci_results.py`](../../scripts/testrail/prepare_ci_results.py) performs these operations:

1. discovers downloaded backend, frontend and E2E JUnit reports;
2. robustly parses both `<testsuite>` and `<testsuites>` roots;
3. creates a valid merged raw backend-integration report;
4. maps every raw testcase to the manifest and fails closed on an unknown selector;
5. emits one aggregate testcase per TestIntent and run area.

Aggregate status is conservative: any bound failure/error makes the TestIntent fail; otherwise any skipped source makes it skipped; only an all-passed set passes. Theory rows are matched by class and base method name, so argument formatting does not change the stable aggregate ID. Renaming or adding a test requires updating the binding manifest in the same pull request.

### Current operational mappings

The current binding map includes two additional operational TestIntents compared with the accepted TECH-08 baseline.

`ESHOP-GW-003` covers Operational Health aggregation through six integration selectors:

* healthy aggregate behavior
* degraded aggregate behavior
* downstream dependency diagnostics
* concurrent probing
* bounded probe timeout
* unreachable-service handling

`ESHOP-NOTIFICATION-002` covers operational Notifications through five integration selectors:

* customer denial
* support cross-customer inspection
* bounded paging
* Order ID filtering
* operational audit metadata

These mappings are Main-owned and contribute to the current Backend Integration aggregate count.

## Reports and artifacts

- Backend: TRX plus JUnit from `JunitXml.TestLogger` 7.1.0 (MTP v1-compatible).
- Frontend: normal Vitest console output plus JUnit from `npm run test:junit`.
- Checkout E2E: Playwright list and HTML reporters plus JUnit; traces, screenshots and video retention are unchanged.
- Integration JUnit is merged structurally, never by concatenating XML text.

List exact IDs in any report with:

```powershell
python scripts/testrail/list_automation_ids.py artifacts/testrail/frontend-unit.junit.xml
```

Run the transformation unit tests with:

```powershell
python -m unittest discover -s scripts/testrail/tests -v
```

## GitHub configuration

Repository secrets:

- `TESTRAIL_USERNAME`
- `TESTRAIL_API_KEY`

Repository variables:

- `TESTRAIL_HOST` — for example `https://mbmor.testrail.io`
- `TESTRAIL_PROJECT` — exact TestRail project name

The reporting job validates that all four values are non-empty without printing their contents. QA-04/TECH-06 adds an explicit publication gate after Change scope, Backend, Frontend and Checkout E2E. Only a non-PR application run with all four required job results equal to `success` can reach TestRail; documentation-only, failed, skipped or cancelled execution fails closed to no publication.

Before upload, a read-only TestRail API preflight verifies that every local aggregate Automation ID exists exactly once. Missing or duplicate IDs stop all publication and are printed explicitly. TRCLI 1.15.1 then runs with `-n`, so it cannot create cases, and closes each created run. The run description links to the originating GitHub Actions execution.

All four independent runs are required as one complete CI publication set:

- `CI #<number> | Backend Unit`
- `CI #<number> | Backend Integration`
- `CI #<number> | Frontend Unit`
- `CI #<number> | Checkout E2E`

## Production failure handling

TestRail publication is a blocking CI job.

Artifact downloads are mandatory, and the aggregated report set must exist as valid XML with the exact current checked-in cardinality before Automation ID preflight or the first TRCLI call.

At source baseline `a8d344a`, the required Main report cardinality is:

```text
12/26/3/4
```

Missing configuration, an upstream failure, a missing or malformed report, cardinality drift, mapping drift, a TestRail outage, or a TRCLI failure prevents or fails publication instead of creating an accepted-looking subset.

Historical cardinalities recorded later in this document describe earlier accepted baselines and must not be used as the current publication contract.

TestRail outage recovery is a controlled workflow re-run. The job does not retry blindly and never reuses a partial run. Because TestRail receives four independent API operations rather than one transaction, an outage during TRCLI calls can still leave an externally partial diagnostic set; that residual must be identified by CI number and excluded from acceptance until a complete rerun passes.

## Initial production acceptance

The pilot was accepted on July 28, 2026 using GitHub Actions run `30356073486` (`CI #28`). The live TestRail verification found zero open and four newly completed runs, all linked to the originating workflow and closed at 100% Passed:

- `R17` / Backend Unit: 12 TestIntent results;
- `R18` / Backend Integration: 27 TestIntent results;
- `R19` / Frontend Unit: 2 TestIntent results;
- `R20` / Checkout E2E: 4 TestIntent results.

The run areas intentionally overlap where several automation layers support the same TestIntent. All individual result rows were assigned to governed suite cases, and the suite remained at 45 cases, confirming that `trcli -n` created no new cases. Following this acceptance, the job-level pilot `continue-on-error` setting was removed and TestRail publication became a blocking quality gate.

## GAP-001 shared evidence

GitHub Actions run [`30429176555`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30429176555) (`CI #35`) completed successfully on July 29, 2026 for commit `1da2ccb`. Quality policy, Backend, Frontend, Container images, Checkout E2E and Publish TestRail results all concluded `success`. The publishing job created four closed, 100% Passed runs linked to that workflow:

- `R45` / Backend Unit: 12 TestIntent results;
- `R46` / Backend Integration: 28 TestIntent results;
- `R47` / Frontend Unit: 3 TestIntent results;
- `R48` / Checkout E2E: 4 TestIntent results.

The 47 aggregate rows deliberately overlap. The broker-delivery selector supports both `ESHOP-INVENTORY-002` and `ESHOP-DATA-002`; both aggregates passed again in `R46` without creating a new case. CI #34/R42 remains the initial immutable shared proof for that selector. Neither execution activates a Future gate or constitutes a release decision.

## Governed Nightly and Release publication

QA-03 defines the source-of-truth classification in [`test-tier-policy.json`](../../scripts/quality/test-tier-policy.json). [`test_tiers.py`](../../scripts/quality/test_tiers.py) fails closed when a TestRail-bound selector is unclassified, ambiguously sourced, unknown in an override or changes the accepted TECH-05 `77/98/19/13` counts.

The dedicated [`quality-tiers.yml`](../../.github/workflows/quality-tiers.yml) runs Nightly on a daily schedule and exposes Nightly/Release via `workflow_dispatch`. It builds an exact project/filter matrix, retains raw TRX/JUnit for 30 days, aggregates only the selected results and publishes one closed TestRail run named `Nightly #<run> | Backend Integration` or `Release #<run> | Backend Integration`.

Validate the governed classification locally before a commit:

```powershell
python scripts/quality/test_tiers.py validate
python scripts/quality/gateway_routes.py validate
python -m unittest discover -s scripts/quality/tests -v
python scripts/quality/test_tiers.py matrix --tier nightly
python scripts/quality/test_tiers.py matrix --tier release
```

The first shared tier acceptance completed on 2026-07-29. GitHub run [`30430788377`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30430788377) published closed TestRail Nightly `R49` with 11/11 Passed; run [`30430855956`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30430855956) published closed Release `R50` with 6/6 Passed. The suite remained at 45 cases, so fail-closed identity matching did not create catalogue drift. PR `CI #37` and Main `CI #38` subsequently accepted the cumulative/direct-push semantics described below.

## Governed PR and Main execution

The governed tier model uses primary tier as selector ownership and cumulative event semantics for safety.

The current checked-in policy defines:

* pull requests execute the 77 PR-owned logical selectors;
* Main owns 113 additional logical selectors;
* pushes to `main` and manual CI dispatch execute PR + Main ownership cumulatively, for 190 logical selectors;
* Nightly remains an independent 19-selector execution rather than being folded into Main;
* Release remains an explicit 17-selector overlap and is not inferred from a normal Main pass;
* Main aggregation produces 32 TestIntents;
* Release aggregation produces 9 TestIntents.

The authoritative values are stored in:

`scripts/quality/test-tier-policy.json`

Documentation must not be treated as the executable source of truth for selector or aggregate counts.

Backend integration projects still restore and compile on pull requests, but their Docker-backed tests do not execute. Container images, Checkout E2E and TestRail publication are also skipped, preventing untrusted PR code from using repository secrets. Main filters are generated from the checked-in policy for the three mixed projects; their approved selector counts are Inventory 14, Messaging 4 and Orders 9.

The CI workflow classifies the complete PR or push diff before expensive jobs start. A non-empty change set containing only files below `docs/` or Markdown files runs the stable Change scope and Quality policy checks but skips Backend, Frontend, Container images, Checkout E2E and TestRail publication. Any application, configuration, script or workflow change — as well as an empty or indeterminate comparison — fails closed to the full event-appropriate pipeline. Manual `workflow_dispatch` always requests the full pipeline. This keeps required workflow checks observable while preventing documentation-only merges from creating redundant TestRail runs.

Fail-closed validation locks both selector and publication cardinality. PR [`CI #37`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433749355) passed only Quality policy, Backend and Frontend; Containers, E2E and TestRail were skipped. Main [`CI #38`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433934594) passed all jobs and published four closed TestRail runs: R55 Backend Unit 12, R56 Backend Integration 22, R57 Frontend Unit 3 and R58 Checkout E2E 4, all Passed. Both event paths are accepted and GAP-022 is complete.

TECH-03/GAP-020 strengthens the four selectors already bound to `ESHOP-DATA-004`; it does not change Automation IDs, mapping edges, selector ownership or report cardinality. Main `CI #42` published R63–R66 at `12/22/3/4`; the existing `[Negative mutations]` TestIntent passed in R64 and GAP-020 is closed.

TECH-04 adds the Catalog `application/problem+json` transport assertion to that same aggregate. PR CI #45 and Main CI #46 passed; Main published R71–R74 at `12/22/3/4`, and the existing `[Negative mutations]` TestIntent passed in R72 without creating a case.

PR CI #47 and Main CI #48 accepted the docs-only gate on `06b8895`. Both ran Change scope and Quality policy only; no TestRail run was created, leaving R71–R74 as the latest publication.

TECH-05 adds `GatewayAuthorizationTests.EveryAddressableRouteEnforcesAuthorizationAndForwarding` to existing `ESHOP-GW-001`. Its 43 xUnit rows aggregate into that one TestIntent, so Main remains exactly `12/22/3/4` result rows. The mapping is 194 selectors/212 edges and cumulative Main 175.

Main CI #50 published partial successful R75–R77 but failed Checkout E2E before startup on the unsupported Ubuntu command `ss --headers=never`; that run group is diagnostic, not acceptance evidence. Hotfix `daf835d` uses portable `ss -ltn`/`ss -ltnp`. Main [`CI #52`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30448986631) passed and published closed R78 Backend Unit 12, R79 Backend Integration 22, R80 Frontend Unit 3 and R81 Checkout E2E 4. All are Passed, R79 contains Passed `ESHOP-GW-001`, `trcli -n` created no case, and TECH-05/GAP-026 is accepted.

QA-04/TECH-06 converts that diagnostic finding into preventive controls. Commit `b259026` requires successful Change scope, Backend, Frontend and Checkout E2E jobs before publication; requires four non-empty, valid reports with exact `12/22/3/4` cardinality before the first TestRail call; and validates the Linux/Windows port-detection shell contract in Quality policy. Main [`CI #56`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30451634130) passed and published closed R82 Backend Unit 12, R83 Backend Integration 22, R84 Frontend Unit 3 and R85 Checkout E2E 4. All are 100% Passed, so QA-04/TECH-06 is accepted without changing TestIntent identity, selector ownership or gate state.

TECH-07 adds `CatalogServiceIntegrationTests.CatalogMutationBoundaryRejectsUnauthorizedCallersWithoutPersistence` and `GatewayAuthorizationTests.CatalogMutationRoutesAreNotAddressableOrForwarded` to `ESHOP-CATALOG-001`; the gateway selector also strengthens `ESHOP-GW-001`. Theory rows aggregate to one result per TestIntent, so the complete Main report contract is `12/23/3/4`. The explicit Release matrix contains 15 selectors and produces 8 aggregate TestIntents over 24 edges. TestRail C53 is synchronized to the code-first Automation ID and both native/custom automation indicators. Local validation, Catalog 19/19, gateway 68/68 and shared publication all pass.

The initial Main publication failed before remote calls because the unchanged manifest schema had been incorrectly labeled version 2 while `junit_tools.py` accepts version 1. Hotfix `4ec560f` restored version 1 and added a production-manifest regression test. Main [`CI #62`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30458083498) then published R86–R89 at `12/23/3/4`, all Passed. Governed Release [`30481512624`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30481512624) published R90 with 8/8 Passed, so TECH-07/GAP-003 is accepted.

TECH-08 adds `BasketServiceIntegrationTests.ReadinessTracksRedisOutageAndRecoveryWhileLivenessStaysHealthy` and `CatalogServiceIntegrationTests.ReadinessTracksPostgreSqlOutageAndRecoveryWhileLivenessStaysHealthy` to existing `ESHOP-RESILIENCE-002`. Both are Main-owned and explicit Release selectors. Validation reports 198 selectors/217 edges, cumulative Main 179, Main `12/24/3/4`, and Release 9 aggregates over 26 edges. C80 is synchronized to `Eshop.TestIntents.ESHOP-RESILIENCE-002`, so `trcli -n` stays fail-closed. Main [`30486673945`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30486673945) published CI #66/R92 with 24/24 Passed, and governed Release [`30487730431`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30487730431) published Release #4/R95 with 9/9 Passed; C80 passed in both.

Release intentionally downloads and publishes only backend-integration reports. The preparation script therefore emits informational GitHub annotations that backend-unit, frontend-unit and checkout-e2e reports are absent and skipped; these are expected for this profile and do not indicate lost Release evidence.
