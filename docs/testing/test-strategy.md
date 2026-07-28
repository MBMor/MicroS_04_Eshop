# Risk-Based Quality Engineering Strategy

> **Document type:** Long-term quality and test strategy  
> **Repository:** `MBMor/MicroS_04_Eshop`  
> **Version:** 1.0  
> **Status:** Draft — pending approval  
> **Effective from:** Not effective; approval establishes the strategy, while every release gate requires a separate explicit activation record  
> **Last approved:** Not yet approved  
> **Last reviewed:** 2026-07-26  
> **Next scheduled review:** 2026-10-26  
> **Supersedes:** 0.1 draft  
> **Primary accountable owner:** QA Architecture  
> **Review cadence:** Quarterly and on material architectural change

## Related documents and authority

- [Quality Gate and Release Evidence Policy](quality-gate-policy.md)
- [Test Engineering Standards](test-engineering-standards.md)
- [TestRail and Traceability Governance](testrail-governance.md)
- [Quality Risk Register](quality-risk-register.md)
- [Quality Traceability Matrix](traceability-matrix.md)
- [Automated Test Gap Analysis](automated-test-gap-analysis.md)
- [Automated Coverage Inventory](automated-coverage-inventory.md)
- [TestRail Suite Design](testrail-suite-design.md)

### Document hierarchy and conflict resolution

The governing order is:

```text
Applicable legal, regulatory and security obligations
→ approved product behavior and domain invariants
→ published external contracts and compatibility commitments
→ approved architectural and operational decisions
→ this Risk-Based Quality Engineering Strategy
→ Quality Gate Policy for release decisions and exceptions
→ Test Engineering Standards for evidence design and execution
→ TestRail Governance for management metadata and traceability
→ current point-in-time artifacts and reports
```

A more specific document governs within its stated normative scope. A lower-level artifact MUST NOT weaken a higher approved obligation. A cross-scope conflict MUST be recorded and resolved by QA Architecture and every materially affected accountable owner before the affected evidence or gate is reported as satisfied. Until resolution, the oracle is `Decision required`, execution is `Blocked` where the missing decision prevents execution, and the release decision retains the exposure.

The Quality Risk Register is the sole authoritative registry for `R-*` and `CTRL-*` identifiers, control ownership, implementation status and evidence-strength claims. Other documents may reference controls but MUST NOT create an authoritative duplicate catalog. The Quality Gate Policy is authoritative for `GATE-*`; TestRail Governance is authoritative for `ESHOP-*` and canonical status values.

---

## 1. Executive summary

This strategy defines the long-term quality engineering model for the MicroS_04_Eshop microservices platform. Product, Engineering, QA, Security and Platform use it to identify quality risk, select evidence, govern confidence and improve the system.

The strategy is implementation-led and risk-based. Source, runtime configuration, migrations, infrastructure definitions, automated tests, immutable artifacts and observed runtime behavior establish what exists. Approved product decisions, invariants, obligations and public contracts define what should exist. Documentation supports, but does not prove, implementation or control effectiveness.

The highest-value evidence is expected at distributed-system boundaries: concurrency and stale state; duplicate, delayed, malformed and out-of-order delivery; ambiguous commit windows; dependency outage; schema and contract evolution; authorization and ownership; production ingress; restoration; alert delivery; diagnostics; and reconciliation.

The model separates:

- intended behavior from current implementation;
- stable principles from point-in-time findings;
- product risk from evidence gaps;
- gate lifecycle from gate activation and decision phase;
- gate satisfaction from residual-risk disposition;
- implemented controls from proven effectiveness;
- product failures from environment failures;
- risk acceptance from gate waiver and engineering-standard deviation;
- stable test intent from automation bindings and append-only execution evidence;
- test intent from the many-to-many mappings between cases and gates;
- deployment authorization from post-deployment release completion.

This strategy is not a certification that its objectives are currently satisfied. In version 1.0 every gate remains `Future` until its activation-wave exit criteria are met and an accountable authority records a concrete effective date or release.

---

## 2. Normative language

- **MUST / MUST NOT / REQUIRED** — mandatory for the applicable, effective approved baseline. Non-conformance requires a blocker, an eligible approved waiver, or evidenced `Not applicable` decision.
- **SHOULD / SHOULD NOT** — expected unless an approved engineering-standard deviation proves equivalent or greater risk reduction.
- **MAY** — optional.
- **Maturity target** — planned capability, not mandatory until promoted and activated.
- **Where applicable** — only after applicability is explicitly evaluated; never an undocumented escape.
- **Where supported** — only when included in the approved baseline. An undecided required capability is a design blocker.

Normative force starts on the strategy effective date. A gate becomes release-blocking only after its own concrete `Effective from` activation. Approval of this strategy or a Future gate does not silently activate that gate.

---

## 3. Purpose and quality objectives

The strategy exists to ensure that:

1. only authorized identities perform allowed actions and access their own data;
2. prices, totals, stock, payments and order state remain correct under concurrency, retry and partial failure;
3. every committed distributed workflow progresses, compensates or reaches an observable terminal failure;
4. duplicate, delayed, malformed and out-of-order messages do not create incorrect side effects;
5. schema and contract changes remain compatible across independently deployed components;
6. deployable artifacts start safely, expose truthful health and work through intended ingress;
7. failures can be reconstructed through trace, correlation, metric and log evidence;
8. backups, alerts and operational procedures work when required;
9. release decisions use explicit gate evidence and separately dispositioned residual risk;
10. automated tests remain deterministic, maintainable and proportionate;
11. TestRail, CI and repository evidence remain traceable without low-value duplication;
12. production incidents and escapes continuously improve the risk model and portfolio.

---

## 4. Quality principles

### 4.1 Risk drives depth

Critical and High risks MUST have direct evidence at the layer where the failure can occur. Evidence depth is proportionate to impact, exposure, detectability, recovery difficulty, execution frequency and evidenced control effectiveness.

### 4.2 Oracle hierarchy

Expected behavior follows the hierarchy at the start of this document. `Oracle approval status` has exactly these values: `Approved`, `Decision required`, `Not applicable`. Risk acceptance is never an oracle state. When sources conflict, record the inconsistency, obtain approval, correct implementation or documentation, then update the oracle.

### 4.3 Controls require effectiveness evidence

Transactions, concurrency tokens, retries, inboxes, outboxes, health checks, backups and alerts reduce residual risk only for material variants proven by valid evidence. `Missing`, `Unknown` and `Indirect` evidence normally provide no score reduction. `Partial` evidence can justify a reduction only for the proven scope.

### 4.4 Invariants and material variants

Assertions SHOULD protect durable properties: no oversell; one logical payment outcome; no silent loss of committed events; no cross-customer access; supported migrations preserve valid data; readiness is false during mandatory dependency outage; replay is idempotent; restored state is consistent.

One example does not prove a class. Aggregate evidence is `Direct` only when every identified material variant has sufficient direct evidence; otherwise it is `Partial`.

### 4.5 Determinism

Concurrency and recovery tests MUST use controlled synchronization, clocks, observable state, bounded polling and deterministic fault injection where feasible. Arbitrary sleeps, reruns and statistical success do not replace deterministic proof.

### 4.6 Lowest sufficient level and selective fidelity

Use the lowest layer able to prove the risk. Real PostgreSQL, Redis and RabbitMQ are REQUIRED when engine semantics matter. Built artifacts and production-like topology are REQUIRED for ingress, network, migration, runtime identity, filesystem, readiness, telemetry, backup, recovery and post-deployment evidence.

### 4.7 First-attempt truth and environment classification

Retries MAY diagnose but MUST NOT convert an initial failure into `Passed`. Missing Docker, broker, database, browser, identity provider, credentials, target or telemetry produces `Environment failed`; a missing approved decision produces `Blocked`. Neither is a product pass.

### 4.8 Gate and risk independence

Except for gates whose explicit intent is risk disposition, gate satisfaction is evaluated independently of residual-risk acceptance. A release decision combines:

1. gate evaluation;
2. residual-risk acceptance status;
3. active gate waivers;
4. active non-waivable defects;
5. deployment phase.

Risk acceptance does not satisfy or waive a gate. A gate waiver does not accept risk. `GATE-RISK-001` is the explicit gate for Critical residual-risk disposition.

### 4.9 Evidence integrity and segregation of duties

Gate evidence MUST be attributable, immutable or tamper-evident, provenance-linked and protected from silent replacement. For Critical gates, manual operational evidence, artifact-equivalence decisions, waivers and engineering-standard deviations, the sole producer/operator MUST NOT be the sole approver. The approver MUST have the accountable authority and sufficient independence to challenge the evidence.

---

## 5. Product and architecture context

The platform comprises React, ASP.NET Core YARP, Catalog, Basket, Orders, Inventory, Payments and Notifications services, Keycloak, PostgreSQL, Redis, RabbitMQ, transactional outboxes and inboxes, OpenTelemetry and Docker-based infrastructure. Checkout is an asynchronous distributed-state workflow.

No tenant model is assumed. Customer ownership remains applicable. Tenant isolation is `Not applicable` only while evidence shows no tenant discriminator, context or enforcement model exists.

---

## 6. Scope

### 6.1 In scope

Product/domain logic; APIs and middleware; gateway; frontend and accessibility; PostgreSQL, Redis and migrations; RabbitMQ topology and recovery; outbox/inbox; duplicates and disorder; compensation, DLQ and reconciliation; authentication, authorization and ownership; security and supply chain; observability and health; containers, deployment and ingress; backup and recovery; CI evidence and waivers; performance, resilience and exploration; TestRail traceability and production feedback.

### 6.2 Decision required

The following require approved contracts before direct testing: checkout idempotency; basket concurrent update semantics; price freshness; inventory fulfillment ownership; 404/403 disclosure; event compatibility/versioning; production ingress/API base; RTO/RPO; support matrix; DLQ replay; reconciliation and repair.

An undecided required capability MUST NOT be `Passed`, `Direct` or `Not applicable`.

### 6.3 Out of scope unless adopted

Real payment-provider certification; fulfillment integrations; fiscal/tax rules; discounts, loyalty, refunds and chargebacks; external cloud controls unavailable as evidence; destructive DR on shared environments; third-party infrastructure penetration testing; tenant isolation before a tenant model exists.

---

## 7. Risk management model

### 7.1 Scoring

Each risk is **numerically scored using Likelihood and Impact** and **qualitatively prioritized using Exposure, Detectability, Recovery difficulty and evidenced Control effectiveness**.

```text
Risk score = Likelihood × Impact
```

Likelihood and Impact use 1–5. Scores: 1–4 Low, 5–9 Medium, 10–16 High, 17–25 Critical. Qualitative dimensions do not silently alter the calculated level.

Within a band, prioritize higher customer/financial/security/data exposure, lower detectability, weaker controls, harder recovery, wider execution and longer remediation lead time. Deviations require the risk owner's rationale.

### 7.2 Inherent, residual and target risk

Every Critical and High risk MUST record:

- inherent Likelihood, Impact and score;
- residual Likelihood, Impact and score;
- target Likelihood, Impact and score or `Decision required`;
- material `CTRL-*` references;
- capability/control implementation status and evidence strength by material variant;
- rationale for every residual-risk reduction;
- one accountable risk owner, responsible treatment team and evidence owner;
- target completion or activation wave;
- risk-acceptance status and reference;
- separate gate-waiver reference where applicable.

`Risk acceptance status` values are `Not required`, `Required`, `Pending`, `Accepted`, `Expired`, `Rejected`. Acceptance is governed in the risk register and never stored as oracle approval.

Direct preventive evidence may reduce Likelihood; direct consequence-limiting or recovery evidence may reduce Impact. QA Architecture and the accountable risk owner approve Critical/High reductions.

### 7.3 Appetite

| Residual risk | Default treatment |
|---|---|
| Critical | Block release unless eligible exposure has separately approved acceptance and, where a gate is unsatisfied, an eligible waiver. Non-waivable defects always block. |
| High | Explicit accountable-risk-owner and QA Architecture disposition, remediation and monitoring. |
| Medium | The accountable owner may accept with treatment and monitoring. |
| Low | Normal backlog and monitoring. |

### 7.4 Review triggers

Review after changes to domain states, invariants, routes, authorization, contracts, topology, migrations, constraints, concurrency, dependencies, deployment, versions, observability, alerts, backups, recovery or threat boundaries; quarterly; and after material incidents or recurring production signals.

---

## 8. Critical workflows

End-to-end traceability is required for: ingress authentication/authorization; isolated basket mutation; atomic order/outbox creation; all-or-nothing inventory reservation; one payment outcome; confirm or compensate exactly once; inventory release; notification privacy; publishing and consumption recovery; reconciliation; populated migration; restoration; built frontend ingress; truthful health/diagnostics; and post-deployment verification.

A browser journey is not proof of persistence, delivery, recovery or concurrency semantics.

---

## 9. Portfolio and change impact

The portfolio is a risk-adjusted pyramid: unit/component; service integration; contract; messaging/workflow; browser; deployment/migration/resilience/performance; operational evidence.

Every material change MUST map affected risks, controls, gates, routes, contracts, events, states, schemas, trust boundaries, artifacts, observability, runbooks and TestRail cases. Shared-component changes fan out. Selective execution may omit a mandatory gate only through proven no-impact or immutable-artifact equivalence retained with evidence.

---

## 10. Reliability, security and production feedback

Product, Engineering and Platform MUST approve measurable SLO/RTO/RPO artifacts with source, window, exclusions, owner and breach action. Before approval, the item is `Decision required`.

Threat modeling covers assets, trust boundaries, attackers, abuse, public/internal ingress, replay and recovery. Scanners supplement threat-derived evidence.

Incidents, escapes, SLO breaches, alert/DLQ trends, reconciliation, support signals and emergency fixes MUST be assessed for changes to risk, controls, tests, gates, runbooks and supported objectives.

---

## 11. Adoption and gate-activation roadmap

No gate is Active in this 1.0 draft. Activation is incremental and requires a separate catalog change with a concrete date or release identifier.

| Wave | Scope | Minimum exit criteria | Gate families eligible for activation |
|---|---|---|---|
| W0 — Governance substrate | Canonical IDs, ownership, evidence and decision records | Unique resolving IDs; exact enums; singular accountable owners; append-only evidence store; defect, risk and vulnerability inventories; approved activation authority | None; this wave enables later activation |
| W1 — Governed baseline | Current deterministic portfolio and release-decision inputs | PR/Main tier split; automation bindings; first-attempt import; provenance; contract-baseline registry; release decision record | `GATE-REL-*`, `GATE-RISK-001`, `GATE-SEC-002`, `GATE-CON-001`, advisory quality/security gates |
| W2 — Critical workflow integrity | Authorization, checkout, inventory, payment and messaging correctness | Close Critical gaps; deterministic concurrency and duplicate/crash evidence; approved missing product oracles | `GATE-SEC-001`, `GATE-INV-001`, `GATE-PAY-001`, `GATE-ORD-001`, `GATE-MSG-001`–`004`, `GATE-SEC-003` |
| W3 — Artifact, data and deployment | Production-relevant images, ingress, migrations, readiness and UI | Immutable RC artifacts; full-stack environment; populated-schema baselines; readiness contract; production-relevant critical journey | `GATE-DATA-001/003`, `GATE-OPS-001`, `GATE-DEP-001/003`, `GATE-UI-001`, `GATE-ACC-001` |
| W4 — Operational and post-deploy | Restore, alerts, recovery objectives, reconciliation, performance and target verification | Calendar-valid operational procedures; post-deploy synthetic and telemetry evidence; rollback/forward-fix ownership | `GATE-DEP-002`, `GATE-DATA-002`, `GATE-OPS-002/003`, `GATE-MSG-005/006`, `GATE-PERF-001` |

A wave does not activate every listed gate automatically. Each gate is activated only after its own prerequisites are met and its catalog row is changed from `Future` to a concrete effective date or release.

---

## 12. Engineering-standard deviations

A deviation permits an alternative to a `SHOULD` or, only where explicitly eligible, a technical execution requirement. It is not a gate waiver and does not change execution or evidence status.

Each `DEV-*` record MUST include the exact clause; scope; alternative; proof of equivalent or greater evidence strength; risks, controls, gates and cases; owner; independent approver; effective/expiry dates; monitoring; and revalidation triggers. A deviation cannot override legal obligations, non-waivable blockers, an approved product oracle or an explicit policy prohibition. If the alternative leaves a mandatory gate unsatisfied, a separate gate waiver is required.

---

## 13. Ownership and decision rights

Every Critical workflow/risk, material control and mandatory gate has exactly one accountable owner. Product approves business semantics; Engineering owns service behavior; Security owns identity/security; Platform owns deployment, restore and operational infrastructure; QA Architecture owns the risk model, evidence framework and canonical schemas; the named release owner makes release decisions.

Responsible teams may be plural, but the accountable owner field MUST contain exactly one role. Critical manual/operational evidence, equivalence, deviations and waivers require an approver other than the sole producer/operator.

---

## 14. Maintenance and approval

Review quarterly, after material incidents/findings or architecture changes, and before promotion/activation of a gate. Point-in-time values remain in audit artifacts. Material changes require QA Architecture and affected-owner approval.

### Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|


### Approval

| Role | Name | Date | Decision |
|---|---|---|---|
| QA Architecture |  |  |  |
| Engineering |  |  |  |
| Product |  |  |  |
| Security |  |  |  |
| Platform |  |  |  |

