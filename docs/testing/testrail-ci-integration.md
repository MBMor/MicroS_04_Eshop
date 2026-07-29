# TestRail CI integration

This repository uses TestRail's code-first JUnit flow with a deliberate aggregation layer. The TestRail suite contains 45 high-level TestIntents, while the automated implementation contains many lower-level xUnit, Vitest and Playwright tests. Raw test cases are therefore retained as CI artifacts, but TestRail receives one synthetic JUnit result per TestIntent.

The committed baseline maps 193 unique source selectors through 211 binding edges to 31 automated TestIntents; the GAP-001 selector binds to both `ESHOP-DATA-002` and `ESHOP-INVENTORY-002`. GitHub Actions `CI #46` on merge commit `b298107` is the latest accepted cumulative Main publication; TestRail `R71`–`R74` contain the locked `12/22/3/4` Passed results. Nightly `R49` and Release `R50` are the first accepted governed-tier publications.

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

The reporting job validates that all four values are non-empty without printing their contents. It runs after backend, frontend and E2E processing even when a test job fails, but never on pull requests. A skipped upstream area produces no TestRail run for that area; available reports are still processed.

Before upload, a read-only TestRail API preflight verifies that every local aggregate Automation ID exists exactly once. Missing or duplicate IDs stop all publication and are printed explicitly. TRCLI 1.15.1 then runs with `-n`, so it cannot create cases, and closes each created run. The run description links to the originating GitHub Actions execution.

Four independent runs are created when their artifacts exist:

- `CI #<number> | Backend Unit`
- `CI #<number> | Backend Integration`
- `CI #<number> | Frontend Unit`
- `CI #<number> | Checkout E2E`

## Production failure handling

TestRail publication is a blocking CI job. Missing configuration, mapping drift, a TestRail outage or a TRCLI failure therefore changes the workflow conclusion to failed. Individual artifact downloads remain tolerant so that a skipped upstream area does not prevent publication of available reports; an absent area creates no empty TestRail run.

TestRail outage recovery is a controlled workflow re-run. The job does not retry blindly and never reuses a partial run.

## Initial production acceptance

The pilot was accepted on July 28, 2026 using GitHub Actions run `30356073486` (`CI #28`). The live TestRail verification found zero open and four newly completed runs, all linked to the originating workflow and closed at 100% Passed:

- `R17` / Backend Unit: 12 TestIntent results;
- `R18` / Backend Integration: 27 TestIntent results;
- `R19` / Frontend Unit: 2 TestIntent results;
- `R20` / Checkout E2E: 4 TestIntent results.

The run areas intentionally overlap where several automation layers support the same TestIntent. All individual result rows were assigned to governed suite cases, and the suite remained at 45 cases, confirming that `trcli -n` created no new cases. Following this acceptance, the job-level pilot `continue-on-error` setting was removed and TestRail publication became a blocking quality gate.

## Latest shared evidence

GitHub Actions run [`30429176555`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30429176555) (`CI #35`) completed successfully on July 29, 2026 for commit `1da2ccb`. Quality policy, Backend, Frontend, Container images, Checkout E2E and Publish TestRail results all concluded `success`. The publishing job created four closed, 100% Passed runs linked to that workflow:

- `R45` / Backend Unit: 12 TestIntent results;
- `R46` / Backend Integration: 28 TestIntent results;
- `R47` / Frontend Unit: 3 TestIntent results;
- `R48` / Checkout E2E: 4 TestIntent results.

The 47 aggregate rows deliberately overlap. The broker-delivery selector supports both `ESHOP-INVENTORY-002` and `ESHOP-DATA-002`; both aggregates passed again in `R46` without creating a new case. CI #34/R42 remains the initial immutable shared proof for that selector. Neither execution activates a Future gate or constitutes a release decision.

## Governed Nightly and Release publication

QA-03 defines the source-of-truth classification in [`test-tier-policy.json`](../../scripts/quality/test-tier-policy.json). [`test_tiers.py`](../../scripts/quality/test_tiers.py) fails closed when a TestRail-bound selector is unclassified, ambiguously sourced, unknown in an override or changes the approved `77/97/19/13` counts.

The dedicated [`quality-tiers.yml`](../../.github/workflows/quality-tiers.yml) runs Nightly on a daily schedule and exposes Nightly/Release via `workflow_dispatch`. It builds an exact project/filter matrix, retains raw TRX/JUnit for 30 days, aggregates only the selected results and publishes one closed TestRail run named `Nightly #<run> | Backend Integration` or `Release #<run> | Backend Integration`.

Validate the governed classification locally before a commit:

```powershell
python scripts/quality/test_tiers.py validate
python -m unittest discover -s scripts/quality/tests -v
python scripts/quality/test_tiers.py matrix --tier nightly
python scripts/quality/test_tiers.py matrix --tier release
```

The first shared tier acceptance completed on 2026-07-29. GitHub run [`30430788377`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30430788377) published closed TestRail Nightly `R49` with 11/11 Passed; run [`30430855956`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30430855956) published closed Release `R50` with 6/6 Passed. The suite remained at 45 cases, so fail-closed identity matching did not create catalogue drift. PR `CI #37` and Main `CI #38` subsequently accepted the cumulative/direct-push semantics described below.

## Governed PR and Main execution

The accepted GAP-022 cutover uses primary tier as selector ownership and cumulative event semantics for safety:

- pull requests execute the PR-owned 77 logical selectors: 64 backend unit and 13 frontend;
- pushes to `main` and manual CI dispatch execute PR + Main ownership, 174 logical selectors in total;
- Nightly remains an independent 19-selector execution rather than being folded into Main;
- Release remains a 13-selector overlap and is not inferred from a normal Main pass.

Backend integration projects still restore and compile on pull requests, but their Docker-backed tests do not execute. Container images, Checkout E2E and TestRail publication are also skipped, preventing untrusted PR code from using repository secrets. Main filters are generated from the checked-in policy for the three mixed projects; their approved selector counts are Inventory 14, Messaging 4 and Orders 9.

The CI workflow classifies the complete PR or push diff before expensive jobs start. A non-empty change set containing only files below `docs/` or Markdown files runs the stable Change scope and Quality policy checks but skips Backend, Frontend, Container images, Checkout E2E and TestRail publication. Any application, configuration, script or workflow change — as well as an empty or indeterminate comparison — fails closed to the full event-appropriate pipeline. Manual `workflow_dispatch` always requests the full pipeline. This keeps required workflow checks observable while preventing documentation-only merges from creating redundant TestRail runs.

Fail-closed validation locks both selector and publication cardinality. PR [`CI #37`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433749355) passed only Quality policy, Backend and Frontend; Containers, E2E and TestRail were skipped. Main [`CI #38`](https://github.com/MBMor/MicroS_04_Eshop/actions/runs/30433934594) passed all jobs and published four closed TestRail runs: R55 Backend Unit 12, R56 Backend Integration 22, R57 Frontend Unit 3 and R58 Checkout E2E 4, all Passed. Both event paths are accepted and GAP-022 is complete.

TECH-03/GAP-020 strengthens the four selectors already bound to `ESHOP-DATA-004`; it does not change Automation IDs, mapping edges, selector ownership or report cardinality. Main `CI #42` published R63–R66 at `12/22/3/4`; the existing `[Negative mutations]` TestIntent passed in R64 and GAP-020 is closed.

TECH-04 adds the Catalog `application/problem+json` transport assertion to that same aggregate. PR CI #45 and Main CI #46 passed; Main published R71–R74 at `12/22/3/4`, and the existing `[Negative mutations]` TestIntent passed in R72 without creating a case.
