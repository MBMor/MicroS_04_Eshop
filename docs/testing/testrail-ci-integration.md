# TestRail CI integration

This repository uses TestRail's code-first JUnit flow with a deliberate aggregation layer. The TestRail suite contains 45 high-level TestIntents, while the automated implementation contains many lower-level xUnit, Vitest and Playwright tests. Raw test cases are therefore retained as CI artifacts, but TestRail receives one synthetic JUnit result per TestIntent.

The current TECH-02 working tree maps 190 unique source selectors through 205 binding edges to 31 automated TestIntents. The last accepted shared publication, `CI #28`, predates TECH-01/TECH-02 and therefore remains evidence for 30 automated TestIntents only.

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
