# TestRail CI integration

This repository uses TestRail's code-first JUnit flow with a deliberate aggregation layer. The TestRail suite contains 45 high-level TestIntents, while the automated implementation contains many lower-level xUnit, Vitest and Playwright tests. Raw test cases are therefore retained as CI artifacts, but TestRail receives one synthetic JUnit result per TestIntent.

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

## Pilot and failure handling

The TestRail job currently has `continue-on-error: true`. This is intentional for the pilot: missing configuration, mapping drift, TestRail outage or TRCLI failure remains visible but does not change the main CI conclusion. Remove that job-level setting only after several successful main-branch/workflow-dispatch runs and agreement that TestRail publication is a blocking quality gate.

For the first pilot, run the workflow manually, verify that no cases were created, inspect all produced runs and compare their result counts with the aggregate JUnit artifacts. TestRail outage recovery is a workflow re-run; the job does not retry blindly and never reuses a partial run.
