# TestRail CI integration

This repository uses TestRail's code-first JUnit flow with a deliberate aggregation layer. The TestRail suite contains 45 high-level TestIntents, while the automated implementation contains many lower-level xUnit, Vitest and Playwright tests. Raw test cases are therefore retained as CI artifacts, but TestRail receives one synthetic JUnit result per TestIntent.

The accepted Main baseline is GitHub Actions `CI #56` on `b259026`; TestRail `R82`–`R85` contain the locked `12/22/3/4` Passed results and accept the QA-04/TECH-06 publication-integrity controls. The TECH-07 working-tree candidate maps 196 unique source selectors through 215 binding edges to 32 automated TestIntents. It activates existing case C53/`ESHOP-CATALOG-001`, raises the pending Main contract to `12/23/3/4`, and leaves the 45-case catalogue unchanged. Nightly `R49` and Release `R50` remain the first accepted governed-tier publications; TECH-07 shared Main/Release evidence is pending.

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

TestRail publication is a blocking CI job. Artifact downloads are mandatory, and the aggregated report set must exist as valid XML with the exact checked-in cardinality before Automation ID preflight or the first TRCLI call (`12/23/3/4` in the TECH-07 candidate; the latest accepted baseline was `12/22/3/4`). Missing configuration, an upstream failure, a missing/malformed report, cardinality drift, mapping drift, a TestRail outage or a TRCLI failure therefore prevents or fails publication instead of creating an accepted-looking subset.

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

The accepted GAP-022 cutover uses primary tier as selector ownership and cumulative event semantics for safety:

- pull requests execute the PR-owned 77 logical selectors: 64 backend unit and 13 frontend;
- pushes to `main` and manual CI dispatch execute PR + Main ownership, 177 logical selectors in the TECH-07 candidate;
- Nightly remains an independent 19-selector execution rather than being folded into Main;
- Release becomes a 15-selector overlap for TECH-07 and is not inferred from a normal Main pass.

Backend integration projects still restore and compile on pull requests, but their Docker-backed tests do not execute. Container images, Checkout E2E and TestRail publication are also skipped, preventing untrusted PR code from using repository secrets. Main filters are generated from the checked-in policy for the three mixed projects; their approved selector counts are Inventory 14, Messaging 4 and Orders 9.

The CI workflow classifies the complete PR or push diff before expensive jobs start. A non-empty change set containing only files below `docs/` or Markdown files runs the stable Change scope and Quality policy checks but skips Backend, Frontend, Container images, Checkout E2E and TestRail publication. Any application, configuration, script or workflow change — as well as an empty or indeterminate comparison — fails closed to the full event-appropriate pipeline. Manual `workflow_dispatch` always requests the full pipeline. This keeps required workflow checks observable while preventing documentation-only merges from creating redundant TestRail runs.

Fail-closed validation locks both selector and publication cardinality. PR [`CI #37`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433749355) passed only Quality policy, Backend and Frontend; Containers, E2E and TestRail were skipped. Main [`CI #38`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433934594) passed all jobs and published four closed TestRail runs: R55 Backend Unit 12, R56 Backend Integration 22, R57 Frontend Unit 3 and R58 Checkout E2E 4, all Passed. Both event paths are accepted and GAP-022 is complete.

TECH-03/GAP-020 strengthens the four selectors already bound to `ESHOP-DATA-004`; it does not change Automation IDs, mapping edges, selector ownership or report cardinality. Main `CI #42` published R63–R66 at `12/22/3/4`; the existing `[Negative mutations]` TestIntent passed in R64 and GAP-020 is closed.

TECH-04 adds the Catalog `application/problem+json` transport assertion to that same aggregate. PR CI #45 and Main CI #46 passed; Main published R71–R74 at `12/22/3/4`, and the existing `[Negative mutations]` TestIntent passed in R72 without creating a case.

PR CI #47 and Main CI #48 accepted the docs-only gate on `06b8895`. Both ran Change scope and Quality policy only; no TestRail run was created, leaving R71–R74 as the latest publication.

TECH-05 adds `GatewayAuthorizationTests.EveryAddressableRouteEnforcesAuthorizationAndForwarding` to existing `ESHOP-GW-001`. Its 43 xUnit rows aggregate into that one TestIntent, so Main remains exactly `12/22/3/4` result rows. The mapping is 194 selectors/212 edges and cumulative Main 175.

Main CI #50 published partial successful R75–R77 but failed Checkout E2E before startup on the unsupported Ubuntu command `ss --headers=never`; that run group is diagnostic, not acceptance evidence. Hotfix `daf835d` uses portable `ss -ltn`/`ss -ltnp`. Main [`CI #52`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30448986631) passed and published closed R78 Backend Unit 12, R79 Backend Integration 22, R80 Frontend Unit 3 and R81 Checkout E2E 4. All are Passed, R79 contains Passed `ESHOP-GW-001`, `trcli -n` created no case, and TECH-05/GAP-026 is accepted.

QA-04/TECH-06 converts that diagnostic finding into preventive controls. Commit `b259026` requires successful Change scope, Backend, Frontend and Checkout E2E jobs before publication; requires four non-empty, valid reports with exact `12/22/3/4` cardinality before the first TestRail call; and validates the Linux/Windows port-detection shell contract in Quality policy. Main [`CI #56`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30451634130) passed and published closed R82 Backend Unit 12, R83 Backend Integration 22, R84 Frontend Unit 3 and R85 Checkout E2E 4. All are 100% Passed, so QA-04/TECH-06 is accepted without changing TestIntent identity, selector ownership or gate state.

TECH-07 adds `CatalogServiceIntegrationTests.CatalogMutationBoundaryRejectsUnauthorizedCallersWithoutPersistence` and `GatewayAuthorizationTests.CatalogMutationRoutesAreNotAddressableOrForwarded` to `ESHOP-CATALOG-001`; the gateway selector also strengthens `ESHOP-GW-001`. Theory rows aggregate to one result per TestIntent, so the complete Main report contract becomes `12/23/3/4`. The explicit Release matrix contains 15 selectors and produces 8 aggregate TestIntents over 24 edges. TestRail C53 is synchronized to the code-first Automation ID and both native/custom automation indicators. Local validation and the 19/19 Catalog plus 68/68 gateway suites pass; shared publication remains pending.
