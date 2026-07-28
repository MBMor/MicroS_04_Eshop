# Quality Gate and Release Evidence Policy

> **Document type:** Normative CI, release gate, evidence and waiver policy  
> **Repository:** `MBMor/MicroS_04_Eshop`  
> **Version:** 1.0  
> **Status:** Draft — pending approval  
> **Effective from:** Not effective; every gate is Future until a separate concrete activation record is approved  
> **Last approved:** Not yet approved  
> **Last reviewed:** 2026-07-26  
> **Next scheduled review:** 2026-10-26  
> **Supersedes:** 0.1 draft  
> **Accountable owner:** QA Architecture  
> **Normative language:** [Risk-Based Quality Engineering Strategy](test-strategy.md#2-normative-language)

This policy is authoritative for `GATE-*`, gate lifecycle, activation, decision phase, evidence validity, release decisions and waivers. The [Quality Risk Register](quality-risk-register.md) is authoritative for `R-*` and `CTRL-*`. Conflict resolution follows the [strategy hierarchy](test-strategy.md#document-hierarchy-and-conflict-resolution).

---

## 1. Policy principles

1. Release decisions use identified risk and valid evidence, never aggregate pass percentage alone.
2. A mandatory gate is satisfied only by applicable, effective, sufficiently strong and valid first-attempt evidence.
3. Missing prerequisites, `Environment failed`, `Blocked`, `Not run`, `Quarantined`, invalid and expired evidence remain visible.
4. Gate satisfaction is independent of residual-risk acceptance except for gates explicitly concerned with risk disposition.
5. Risk acceptance and gate waiver are separate records and approvals.
6. All active S1 and P0 defects block release by default. Only defects outside the non-waivable classes may be considered for an exceptional waiver under the full process. Confirmed active non-waivable S1/P0 defects can never be waived or accepted into release.
7. Evidence identifies immutable artifacts, environment and decision phase.
8. `Not applicable` proves absence of capability/risk; a design blocker is never `Not applicable`.
9. A Future gate is not mandatory before its concrete activation date or release.
10. Approval of this policy does not activate any gate.
11. Deployment authorization and release completion are distinct decisions.

---

## 2. Gate dimensions

### 2.1 Lifecycle

`Baseline mandatory`, `Conditional mandatory`, `Advisory`, `Maturity target`, `Retired`.

Lifecycle changes record old/new values, rationale, affected risks, evidence, owner, effective date, CI/environment readiness and approvals. Mandatory gates are not demoted because they fail or cost too much.

### 2.2 Activation status

Derived from the catalog `Effective from` field:

- `Future` — approved or proposed but not yet effective;
- `Active` — a concrete date or release has been reached;
- `Retired` — no longer used, with retained successor and history.

`Future — explicit activation record required` is complete Future metadata, not missing metadata. It becomes Active only through a catalog change that records a concrete date or release and confirms the wave exit criteria.

Only an Active Baseline mandatory gate or an Active applicable Conditional mandatory gate blocks its decision phase. A missing activation field is a governance blocker and the gate is `Not evaluated`, never silently Active.

### 2.3 Decision phase

| Value | Decision controlled |
|---|---|
| Pre-merge | Change may merge. |
| Pre-deployment | Candidate may be deployed to the target environment. |
| Post-deployment completion | Deployed release may be declared complete/successful. |
| Periodic operational | Time-based operational certification remains valid. |

One gate has one primary decision phase. Evidence may be produced earlier only under approved artifact and environment equivalence.

### 2.4 Derived gate significance

Gate significance is **not stored**. It is derived:

| Lifecycle / applicability / activation | Derived significance |
|---|---|
| Active Baseline mandatory | Mandatory |
| Active Conditional mandatory + Applicable | Mandatory |
| Conditional + Not applicable | None for this scope |
| Advisory | Advisory |
| Future Maturity target, Future mandatory or Retired | None |

This eliminates contradictory combinations such as `Maturity target` plus `Mandatory`.

---

## 3. Gate catalog and activation metadata

IDs are permanent and never reused. Every catalog row has one accountable owner. Responsible teams may be recorded in implementation plans, but do not replace the accountable owner.

### 3.1 Baseline mandatory

| Gate ID | Intent | Wave | Effective from | Phase | Tier | Waiver eligibility | Validity | Accountable owner |
|---|---|---|---|---|---|---|---|---|
| `GATE-INV-001` | No inventory oversell; all material deterministic variants | W2 | Future — explicit activation record required | Pre-deployment | Release | Eligible only for evidence uncertainty; never known oversell | RC | Inventory Engineering owner |
| `GATE-SEC-001` | No cross-customer access and no authorization bypass through gateway/addressable services | W2 | Future — explicit activation record required | Pre-deployment | Release | Never for confirmed bypass | RC | Security owner |
| `GATE-PAY-001` | One logical payment outcome under concurrency, duplicate and replay | W2 | Future — explicit activation record required | Pre-deployment | Release | Never for confirmed duplicate outcome | RC | Payments Engineering owner |
| `GATE-ORD-001` | Valid checkout transitions, idempotent creation, terminal immutability and compensation | W2 | Future — explicit activation record required | Pre-deployment | Release | Eligible unless a non-waivable active defect exists | RC | Checkout workflow owner |
| `GATE-MSG-001` | Atomic outbox and eventual publish or observable terminal failure | W2 | Future — explicit activation record required | Pre-deployment | Release | Never for confirmed silent loss | RC | Shared Messaging owner |
| `GATE-MSG-002` | Sequential and concurrent duplicate safety for side-effecting consumers | W2 | Future — explicit activation record required | Pre-deployment | Release | Eligible unless duplicate financial or stock effect is confirmed | RC | Shared Messaging owner |
| `GATE-MSG-003` | Business-commit-before-ack recovery with one logical effect | W2 | Future — explicit activation record required | Pre-deployment | Release | Eligible unless unsafe recovery is confirmed | RC | Shared Messaging owner |
| `GATE-MSG-004` | Claim exclusivity, non-stealing and stale recovery | W2 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC | Shared Messaging owner |
| `GATE-DATA-001` | Populated supported-schema upgrade and post-upgrade operations | W3 | Future — explicit activation record required | Pre-deployment | Release | Never when a required upgrade is known broken | RC | Data Migration owner |
| `GATE-OPS-001` | Truthful dependency-aware readiness and recovery | W3 | Future — explicit activation record required | Pre-deployment | Release | Never for knowingly false healthy readiness | RC | Platform owner |
| `GATE-DEP-001` | Built-image startup, runtime identity, health and intended ingress | W3 | Future — explicit activation record required | Pre-deployment | Release | Eligible unless the capability cannot serve | RC | Platform owner |
| `GATE-UI-001` | Production-relevant authenticated critical Chromium journey | W3 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC | Frontend Engineering owner |
| `GATE-REL-001` | No unresolved active S1 defect | W1 | Future — explicit activation record required | Pre-deployment | Release | Only outside non-waivable classes under the exceptional process | RC | Release owner |
| `GATE-REL-002` | No unresolved active P0 defect | W1 | Future — explicit activation record required | Pre-deployment | Release | Only outside non-waivable classes under the exceptional process | RC | Release owner |
| `GATE-RISK-001` | No unapproved Critical residual risk | W1 | Future — explicit activation record required | Pre-deployment | Release | Eligible only with separately approved risk acceptance and gate waiver | RC | Release owner |
| `GATE-SEC-002` | No unapproved exploitable Critical vulnerability | W1 | Future — explicit activation record required | Pre-deployment | Release | Never for an active exploitable unmitigated Critical vulnerability | RC | Security owner |
| `GATE-DEP-002` | Target digest, schema, health, ingress, synthetic and operational verification | W4 | Future — explicit activation record required | Post-deployment completion | Post-deploy | Eligible only for bounded evidence unavailability; never unexplained product failure | RC | Release owner |

`GATE-DEP-002` does not prevent initial deployment. After activation, Pre-deployment gates produce `Deployment authorized`; `GATE-DEP-002` then controls `Release completed`.

### 3.2 Conditional mandatory

| Gate ID | Intent and applicability | Wave | Effective from | Phase | Tier | Waiver | Validity | Accountable owner |
|---|---|---|---|---|---|---|---|---|
| `GATE-MSG-005` | DLQ replay safety when replay is in the approved baseline | W4 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC | Workflow Operations owner |
| `GATE-MSG-006` | Reconciliation when critical asynchronous states require detective recovery | W4 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC | Workflow Engineering owner |
| `GATE-DATA-002` | Restore when a release-critical store is due or materially invalidated | W4 | Future — explicit activation record required | Periodic operational | Operational | Eligible only inside an approved contingency | Calendar | Platform owner |
| `GATE-DATA-003` | Rolling compatibility when rolling deployment is supported | W3 | Future — explicit activation record required | Pre-deployment | Release | Eligible unless rollout is known incompatible | RC | Data Migration owner |
| `GATE-SEC-003` | Token validation matrix after identity/trust changes or periodic due date | W2 | Future — explicit activation record required | Pre-deployment | Release | Eligible unless bypass is confirmed | RC/Calendar | Security owner |
| `GATE-OPS-002` | Alert trigger and delivery when a release-critical alert exists or changes | W4 | Future — explicit activation record required | Periodic operational | Operational | Eligible | Calendar | Observability owner |
| `GATE-OPS-003` | Approved recovery-objective acceptance when in scope | W4 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC/Calendar | Platform owner |
| `GATE-PERF-001` | Performance acceptance after a material capacity or workload change | W4 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC | Performance owner |
| `GATE-ACC-001` | Critical-journey accessibility after change or periodic due date | W3 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC/Calendar | Frontend Engineering owner |
| `GATE-CON-001` | Contract compatibility when HTTP, event, router or supported-window changes | W1 | Future — explicit activation record required | Pre-merge | PR/Release | Eligible with an explicit compatibility exception | RC | Contract owner |
| `GATE-DEP-003` | Rollback or forward-fix evidence when compatibility is affected | W3 | Future — explicit activation record required | Pre-deployment | Release | Eligible | RC | Release owner |

Applicability is evaluated every release as `Applicable`, `Not applicable` or `Unknown`. `Unknown` plus a missing decision is blocking only after the conditional gate is Active; before activation the contribution remains `Not evaluated`.

### 3.3 Advisory

| Gate ID | Intent | Wave | Effective from | Phase | Tier | Accountable owner |
|---|---|---|---|---|---|---|
| `GATE-QUAL-001` | Flake, retry and quarantine trend | W1 | Future — explicit activation record required | Pre-deployment | Main/Release | QA Automation owner |
| `GATE-PERF-002` | Performance smoke | W3 | Future — explicit activation record required | Pre-deployment | Nightly | Performance owner |
| `GATE-SEC-004` | Expanded non-blocking security scan | W1 | Future — explicit activation record required | Pre-deployment | Nightly | Security owner |
| `GATE-OPS-004` | Backlog, DLQ, alert and SLO trend review | W4 | Future — explicit activation record required | Pre-deployment | Release | Platform owner |

An advisory finding may create a separately blocking Critical or High risk.

### 3.4 Maturity targets

| Gate ID | Target | Promotion prerequisite | Effective from | Phase |
|---|---|---|---|---|
| `GATE-ACC-002` | Full WCAG 2.2 AA scope | Approved scope, manual protocol, automation and ownership | Future | Pre-deployment |
| `GATE-COMP-001` | Full cross-browser matrix | Approved support matrix and stable environment | Future | Pre-deployment |
| `GATE-SEC-005` | Full supply-chain enforcement | Severity, reachability, license, SBOM and exception policy | Future | Pre-merge |
| `GATE-PERF-003` | Automated regression thresholds | Workload, baseline, variance and capacity environment | Future | Pre-deployment |
| `GATE-OPS-005` | Fully automated alert validation | Safe environment and delivery verification | Future | Periodic operational |
| `GATE-DATA-004` | Automated restore certification | Isolated restore environment, retention and audit | Future | Periodic operational |
| `GATE-MSG-007` | Automated reconciliation effectiveness | Repair policy, false-positive controls and safe automation | Future | Pre-deployment |
| `GATE-RES-001` | Chaos and repeated dependency flap | Deterministic fault platform and limits | Future | Pre-deployment |
| `GATE-DEP-004` | Multi-platform images | Approved platform and architecture matrix | Future | Pre-deployment |

Promotion requires lifecycle-change approval and a concrete activation date or release. It MUST NOT be backdated.

### 3.5 Activation waves and exit criteria

| Wave | Required substrate | Exit criteria before any gate in the wave can activate |
|---|---|---|
| W0 | Governance foundations | Canonical registry validation, singular accountable owners, append-only evidence, defect/risk/vulnerability inventories and approval authority exist. |
| W1 | Governed current baseline | PR/Main tiers, automation bindings, first-attempt result import, immutable provenance, contract baseline registry and release-decision record operate successfully. |
| W2 | Critical workflow integrity | Required product oracles are approved and Critical checkout, authorization, inventory, payment, duplicate, outbox and claim gaps have deterministic direct evidence. |
| W3 | Artifact, data and deployment | Immutable full-stack RC environment, populated migration baselines, truthful readiness, supported ingress and production-relevant Chromium evidence exist. |
| W4 | Operational and post-deploy | Restore, alert, recovery-objective, reconciliation, performance and target-environment procedures have valid calendar or RC evidence. |

The wave field is sequencing metadata, not activation. A gate remains Future until its own concrete `Effective from` value is approved.

### 3.6 Current activation state

At version 2.1 **all gates are Future**. Therefore this draft introduces no release-blocking gate. Current gaps remain visible through evidence strength, execution status, risk disposition and activation prerequisites.

---

## 4. CI tiers

- **PR:** restore/build/static analysis, unit/component, selected service/gateway, contracts, migration drift, secret/dependency scan, orchestration syntax, ID/schema validation and change-impact selection.
- **Main:** full service integration, canonical messaging, built-image smoke, critical Chromium, migration from zero, security ownership matrix and evidence publication.
- **Nightly:** full retries/DLQ, replay/reconciliation, deterministic concurrency, recovery, stale claims/crash windows, cross-browser/accessibility automation, flake/performance/security expansion.
- **Release:** immutable built-artifact full stack, populated migration, security/token, readiness/recovery, due restore, performance/RTO/RPO, alert/runbook, accessibility, exploration, residual-risk/waiver review and rollback/forward-fix.
- **Post-deploy:** target digest/schema/ingress/synthetic/telemetry validation.

Every mandatory gate states required tier and evidence reuse. A different artifact does not satisfy an RC gate without approved equivalence.

---

## 5. Entry and selection

Executions require source/build identity, image digest where applicable, isolated environment, schema, dependency versions, controlled data, health, credentials, approved oracle and compatibility baseline. Test changes invalidate protected evidence unless proven non-semantic.

Impact maps cover risks, controls, gates, routes, contracts, events, states, schema, trust, deployment and observability. Uncertain impact defaults to broader evidence.

---

## 6. Contract baseline governance

Every governed HTTP/event contract MUST reference an immutable released baseline with:

- baseline ID, component/contract, represented release and artifact checksum;
- protected storage location and retention;
- owner and independent approval for replacement;
- supported producer/consumer version window and overlap;
- approved change classification and exception procedure;
- update record linking old/new baselines and compatibility evidence.

CI MUST compare the generated current contract to the approved released baseline, not merely another branch. Replacement is append-only or versioned; no job or user may silently overwrite the previous approved baseline. Emergency updates require contract owner and affected-consumer approvals and retained pre/post artifacts.

---

## 7. Evidence identity and integrity

Gate evidence includes commit, build/attempt, artifact digest, schema, configuration identity, environment/topology, dependencies, test revision, first/retry outcomes, time, owner, `R-*`/`CTRL-*`/`GATE-*`/`ESHOP-*` and decision phase. Manual evidence adds operator, runbook version, start/end and observations.

Release evidence runs against RC immutable artifacts or has approved equivalence covering artifacts, schema, configuration and dependencies. For Critical gates, the equivalence approver cannot be the sole evidence producer.

Evidence is invalidated by material changes to code/tests/oracle/config/schema/contracts/auth/runtime/images/deployment/telemetry/backups/workload or integrity. Original results are never overwritten; corrections append identity, rationale and approver.

---

## 8. Validity profiles and retention

- **RC:** valid only for the candidate or approved immutable equivalent.
- **Calendar:** valid until its maximum age or earlier invalidation trigger.
- **RC/Calendar:** both artifact equivalence and maximum-age requirements apply; the stricter condition wins.

### 8.1 Calendar validity catalog

| Profile | Used by | Maximum age | Early invalidation triggers |
|---|---|---:|---|
| `CAL-RESTORE-90D` | `GATE-DATA-002` | 90 days | backup topology, schema, retention, encryption, restore tooling or critical-store change |
| `CAL-TOKEN-90D` | `GATE-SEC-003` | 90 days | Keycloak realm/client/mapper, signing keys, JWT middleware, trust or clock policy change |
| `CAL-ALERT-90D` | `GATE-OPS-002` | 90 days | alert rule, routing, receiver, credentials, telemetry pipeline or ownership change |
| `CAL-RECOVERY-180D` | `GATE-OPS-003` | 180 days | topology, dependency, workload, retry/recovery mechanism or objective change |
| `CAL-ACCESSIBILITY-90D` | `GATE-ACC-001` | 90 days | critical journey, design system, routing, keyboard/focus or supported-browser change |

A case-to-gate mapping MUST reference the applicable calendar profile. A stricter area policy may shorten the age; it may not extend it without policy approval.

Retention schedules cover tier results, first attempts, recovery/security/performance evidence, waivers, deviations, approvals and sensitive-data handling.

---

## 9. Gate evaluation

Canonical values: `Satisfied`, `Not satisfied`, `Waived`, `Not applicable`, `Not evaluated`.

A gate is `Satisfied` only when it is Active; applicability is resolved; oracle is Approved; required capability/controls exist; all gate-specific material variants have sufficient valid evidence; required first attempt passed; no protecting evidence is quarantined; and gate-specific defect conditions are met.

Residual risk is **not** a general gate-satisfaction criterion. It is a separate release input. The exception is `GATE-RISK-001`, whose intent is residual-risk disposition.

`Waived` means `Not satisfied` plus an eligible, approved, unexpired exception for stated scope; it is not a pass. Future gates are `Not evaluated`. Post-deployment gates are not required for `Deployment authorized` but are required for `Release completed` after activation.

---

## 10. Risk acceptance, waiver and deviation

- **Risk acceptance:** retains known residual risk; never changes execution/evidence/gate status.
- **Gate waiver:** temporary authorization while an eligible mandatory gate is Not satisfied; never proves a control or permanently accepts risk.
- **Engineering-standard deviation:** approves an equivalent alternative method; never by itself waives an unsatisfied gate.

Where needed, all three records are separate, independently approved and linked.

A waiver records ID, gates/risks, score/variants, deficiency, confirmation of no non-waivable defect, business scope, impacts, proven compensating controls, monitoring, rollback/forward-fix, owner, approvers, remediation, effective/expiry. Critical exposure requires Product, Engineering, QA Architecture and relevant Security/Platform. The sole producer/operator cannot be sole approver.

---

## 11. Defects and non-waivable blockers

Severity: S1 data/security/financial loss, oversell, duplicate logical payment, unrecoverable workflow or system-wide Critical failure; S2 major unavailable/incorrect workflow; S3 contained issue with workaround; S4 cosmetic/low impact. Priority: P0 immediate stop; P1 current release; P2 near-term; P3 backlog.

**All active S1 and P0 defects block release by default after the relevant release-decision gates activate.** An exceptional waiver may be considered only when the defect is outside every non-waivable class and the full waiver process proves bounded exposure. The following are never waivable:

- order/inventory/payment corruption or unrecoverable loss;
- duplicate logical charge/payment outcome;
- known oversell;
- cross-customer access or exploitable authorization bypass;
- silent committed critical-event loss;
- required supported populated-schema upgrade failure;
- exploitable unmitigated Critical vulnerability;
- knowingly false healthy readiness during mandatory dependency failure;
- unrecoverable critical workflow or unsafe operational action with material impact.

These must be fixed or removed from scope through an approved product change with applicable evidence.

---

## 12. Flakiness and quarantine

First failure remains authoritative. Classify product/test/environment/infrastructure/unknown. Quarantine maximum is 14 days and records owner, issue, initial evidence, cause, protected risks/gates, start/expiry and remediation. Quarantined evidence cannot satisfy a mandatory gate. Extensions require QA Architecture and risk owner.

---

## 13. Release decisions

### 13.1 Deployment authorized

All Active Pre-deployment baseline/applicable conditional gates are Satisfied or validly Waived; no non-waivable blocker; no unapproved Critical residual risk; High gaps dispositioned; candidate identity fixed; pre-deployment evidence complete.

### 13.2 Release completed

After deployment, all Active Post-deployment completion gates are Satisfied or validly Waived; intended digest/schema/configuration serves traffic; synthetic and operational signals are acceptable; no unexplained failure remains.

If `GATE-DEP-002` fails, execute approved rollback/forward-fix. Deployment-tool success alone never completes a release.

### 13.3 Periodic operational certification

Calendar evidence is current, not invalidated and owned. Expiry makes the relevant conditional gate Not satisfied when applicable.

Reports separate applicability, lifecycle, activation, phase, gate evaluation, first attempt/retries, validity, risk acceptance, waiver and deviation.

---

## 14. Change log and approval

| Version | Date | Material change | Approved by |
|---|---|---|---|


| Role | Name | Date | Decision |
|---|---|---|---|
| QA Architecture |  |  |  |
| Engineering |  |  |  |
| Product |  |  |  |
| Security |  |  |  |
| Platform |  |  |  |

