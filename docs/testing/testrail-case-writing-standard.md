# TestRail Case Writing Standard

> **Document type:** Repository-specific authoring standard  
> **Version:** 1.1  
> **Status:** Active — applied and verified across the 45-case catalogue  
> **Effective from:** July 29, 2026  
> **Scope:** TestRail project `Eshop Quality Engineering`, suite 7 (`Master`)

This standard makes a TestRail case executable and reviewable by a person who knows the product but did not author the automation. It complements [TestRail Suite Design](testrail-suite-design.md); it does not change stable references, automation bindings or execution evidence.

## 1. Reader outcome

After reading one case, a tester or reviewer must be able to answer:

1. Why the case exists and who owns the decision.
2. What environment and data are required.
3. Which action is performed and what is observed after each action.
4. How long an asynchronous result may take.
5. How the environment is restored and what evidence is safe to retain.

A case is not ready when its steps only say to run automation, inspect the implementation, or verify an unspecified correct result.

## 2. Required structure

### Title

Use the stable behavior format:

```text
[Capability] action or condition → observable durable outcome
```

Keep implementation details out of the title unless they are the risk being tested.

### Preconditions

Use short visual blocks rather than one prose wall. Render `OWNER` and `EVIDENCE STRENGTH` as compact labelled lines. Put `PURPOSE`, `SETUP`, `TEST DATA`, optional `ILLUSTRATIVE EXAMPLE` and `CLEANUP` on their own lines, followed by one short paragraph or a list of material values. Keep `BOUNDED WAIT` compact when it fits on one line.

Include the following information when applicable:

- `Owner:` accountable role for the expected behavior.
- `Evidence strength:` `Direct`, `Partial` or `Missing`; this describes current evidence, not desired coverage.
- `Purpose:` one sentence linking the scenario to its product or operational risk.
- `Setup:` services, dependencies and controllable test mechanisms that must already exist.
- `Test data:` identities, records and material variants; use unique, case-owned data.
- `Bounded wait:` the maximum wait and observed condition, or `Not applicable` for synchronous cases.
- `Cleanup:` how shared state is restored and which sensitive data must not be retained.

Do not leave a historical `Decision required` note after the oracle has been approved. Evidence strength may remain `Partial` when the binding covers only part of the intent.

### Steps and expected results

Use three to six paired steps. Each step must contain one coherent tester action and its immediately observable oracle.

For scanability inside TestRail's two-column step table:

- Put the primary action in the first paragraph.
- Put an optional `Illustrative example:` block after a blank line.
- Split statuses, counts and invariants into short bullet lines.
- Group expected results under descriptive labels such as `Expected response:`, `Expected side effects:`, `Expected persisted state:` or `Cleanup oracle:`.
- When an imported action only says to execute material variants, add a `VARIANTS` block containing the case-specific values from `TEST DATA`; preparation alone is not an executable instruction.
- Avoid a paragraph longer than three visual lines at normal desktop width.

- Start actions with a verb such as `Send`, `Pause`, `Load`, `Poll`, `Inspect` or `Confirm`.
- State exact statuses, state transitions, counts or invariants where the contract defines them.
- For asynchronous behavior, name the persisted or external signal and its time bound.
- Include negative side effects when they matter: for example, a denied gateway request is not forwarded.
- Put cleanup in the final step when failure before cleanup could contaminate another test.
- Describe material variants inside the applicable step instead of copying nearly identical cases.

Avoid vague or implementation-only wording such as `run the test`, `check everything`, `works correctly`, `validate DB`, or `see source code`.

### Illustrative examples

Add a stable example when it materially reduces ambiguity. Put `Illustrative example:` in Preconditions for the shared data and repeat only the relevant value in the applicable action or expected result.

- Prefer a representative input/output, state transition, role/endpoint combination or boundary value.
- Use values already protected by the executable binding or product contract.
- Say which identifiers must be generated uniquely per run.
- For nondeterministic concurrency, show an acceptable example without prescribing the winner.
- Keep the exhaustive parameter matrix in its authoritative registry or automation; one or two representative rows are enough in TestRail.
- Do not use real credentials, tokens, customer data, volatile GUIDs/timestamps or a copied implementation payload.

An example is optional when the step already supplies an exact concrete sequence, such as `/ready: 200 Healthy -> 503 Unhealthy -> 200 Healthy`. More prose is not automatically more readable.

## 3. Automated cases

An automated case still needs human-readable instructions. The TestRail case describes the durable intent and oracle; the Automation ID points to the executable binding.

- Keep `Stable Reference`, `Automation ID`, `Is Automated`, `Automation Status`, risk/control/gate IDs and references unchanged during a readability-only edit.
- Do not paste source code, test method names, secrets or volatile CI run IDs into steps.
- If several executable tests implement one intent, describe their combined material behavior and keep the binding details in `Automation Binding` or repository mappings.
- CI results remain append-only execution evidence and do not replace the case oracle.

## 4. Review checklist

A readability edit is accepted only when all answers are `Yes`:

- Can another tester prepare the scenario without opening the test source?
- Does every action have a concrete, observable expected result?
- Can the reader scan action, example and oracle without parsing a wall of text?
- Are asynchronous waits bounded and based on a real signal rather than a sleep?
- Are isolation, cleanup and sensitive-data rules explicit?
- Does the wording match the implemented product contract?
- Are stable identity, traceability fields and automation binding preserved?
- Is the old execution history still attached to the same TestRail case?

## 5. Rollout record

The initial pilot covers five materially different automated intents:

| Case | Stable reference | Pattern exercised |
|---|---|---|
| C51 | `ESHOP-GW-001` | generated security matrix, representative route/role rows and denial non-forwarding |
| C60 | `ESHOP-ORDER-002` | sequential/concurrent idempotency, representative checkout data and conflict |
| C63 | `ESHOP-INVENTORY-002` | deterministic database/broker concurrency and a nondeterministic-winner example |
| C76 | `ESHOP-E2E-001` | distributed checkout workflow, exact monetary example and browser binding |
| C80 | `ESHOP-RESILIENCE-002` | dependency outage, liveness/readiness split and recovery |

After visual acceptance of the pilot, the same structure was applied in place to the remaining 40 cases. The rollout retained each case's current evidence strength, automation state, implementation state and decision status. Manual, Planned, Missing/Partial evidence and `Decision required` cases were not promoted by the readability edit.

The completed UI audit covered C49–C93 and confirmed all 45 expected `ESHOP-*` References, labelled Preconditions, bullet-structured action/oracle pairs, both automation metadata fields and removal of the generic import phrases. The 39 catalogue-import cases outside C53 and the five custom pilot cases expose their case-specific execution data again under `VARIANTS`; C53 and the pilot cases retain their more precise custom actions. All 45 Tests & Results pages remained reachable through the same C IDs. Cases were rewritten in place; none was cloned or renumbered.
