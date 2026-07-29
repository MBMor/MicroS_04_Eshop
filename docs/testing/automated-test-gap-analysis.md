# Automated Test Gap Analysis

> **Document type:** Point-in-time test investment roadmap input  
> **Version:** 2.5
> **Effective from:** 2026-07-28 evidence refresh
> **Baseline:** `main` / `06b8895`; TECH-05/GAP-026 evidence is local and pending shared acceptance
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Analysis date:** 2026-07-29 (Europe/Prague)

The original point-in-time audit started from a clean working tree. Evidence cites repository-relative files and symbols. Gap severity is test-investment priority, separate from product risk. Cost: XS current isolated fixture; S small extension; M new fixture/container scenario; L substantial cross-service/environment work; XL major platform/architecture prerequisite.

Canonical governance fields are explicit: missing approved behavior is `Oracle approval status: Decision required`; planned tests are `Evidence strength: Missing`, never passing coverage. Because all gates are Future in version 2.1, gap rows describe activation prerequisites rather than current blocking gate evaluations. Risk acceptance is not implied.

## Prioritized gaps

| Gap / priority | Risk and evidence gap | Oracle / controls / gates | Recommended direct evidence | Layer / cost / tier / sequencing |
|---|---|---|---|---|
| `GAP-001` Critical — implementation and shared material-variant evidence complete; longitudinal residual | `R-INVENTORY-001`; three deterministic direct PostgreSQL variants passed CI #31/TestRail R30. The two-host/two-consumer RabbitMQ variant proves last-unit contention, bounded retry, exact downstream cardinality and no main/DLQ residue; it passed CI #35/R46 plus first governed Nightly R49 and Release R50 execution. | Oracle Approved; `CTRL-DATA-CONCURRENCY-001` is Direct for the named variants; `GATE-INV-001` W2 activation prerequisite remains Future. | Accumulate scheduled repeat history and review any retry/flake signal. | Remaining longitudinal evidence XS; Nightly+Release. |
| `GAP-002` Critical — implementation and shared material-variant evidence complete; longitudinal residual | `R-ORDER-001`; ten API/frontend variants plus QA-02 sequential and synchronized-concurrent complete-workflow variants passed on commit `a1fba95` in CI #33/TestRail R38 and in first governed Nightly R49/Release R50 execution. Local Messaging 13/13, Orders 17/17 and concurrent 5/5 support determinism. | Oracle Approved in [ADR 0002](../architecture/0002-checkout-command-idempotency.md); `CTRL-ORDER-IDEMPOTENCY-001` is Direct for the named variants and contributes to Future `GATE-ORD-001`. | Accumulate scheduled repeat history. | Remaining longitudinal evidence XS/S; Nightly+Release. |
| `GAP-003` High | `R-AUTH-001`; Catalog mutation actions lack direct auth boundary. | Network oracle Decision required; `CTRL-SEC-CATALOG-BOUNDARY-001` Missing; `GATE-SEC-001` W2 activation prerequisite. | From deployable network, direct POST/PUT/DELETE anonymously is unreachable or 401/403; gateway mutations no-route/not forwarded. | Security/deployment; M; PR service auth + Release network. |
| `GAP-004` High | `R-RESILIENCE-001`; parameterless health checks/status-only tests preserve false-positive health. | Dependency list Decision required; `CTRL-OPS-READINESS-001` Not implemented; `GATE-OPS-001` W3 activation prerequisite. | `/live` and `/ready`; stop DB/Redis/Rabbit/downstream; live follows contract, ready 503 with dependency, then recovers. | Component/deployment; M; Main+Release; contract first. |
| `GAP-005` High | `R-OUTBOX-001/002`; no concurrent claims, stale reclaim, crash window, max retry or cleanup assertions. | Oracles Approved; outbox/claim/publish controls Partial/Indirect; `GATE-MSG-001/004`. | Two publishers disjoint claims/one logical publish; kill after claim and advance time; reclaim; force Dead with attempts/error; cleanup only eligible rows; publish-before-mark recovery. | PG/Rabbit/injected time/hooks; M/L; Nightly→Release; deterministic hooks first. |
| `GAP-006` High | `R-MSG-001`; only StockReservationFailed duplicate covered. | Approved idempotency; `CTRL-MSG-INBOX-001` Partial; `GATE-MSG-002/003`. | Data-driven sequential/concurrent/commit-before-ack duplicates for all side-effecting consumers; one inbox/domain/outbox/notification/payment, ack duplicate, no DLQ. | Messaging; M; Nightly; payment/release/auth paths first. |
| `GAP-007` High | `R-PAYMENT-001`; operational application versus async processor collision. | Oracle Decision required; `CTRL-PAY-UNIQUE-001` Partial; `GATE-PAY-001`. | Operational payment then same-order message, inverse and concurrent order; exactly one payment, defined result event/order state, no poison DLQ. | Payment+messaging; M; Nightly+Release; decide endpoint semantics first. |
| `GAP-008` High | `R-ORDER-002`; Redis price trusted; no freshness/decimal policy. | Oracle Decision required; `CTRL-ORDER-PRICE-001` Partial; `GATE-ORD-001` supporting. | Change catalog price after basket add; assert approved reprice/reject or quoted-expiry policy; max quantity/scale/rounding/multiline exact persisted totals. | Contract/API; S/M; Main+Release; Product decision first. |
| `GAP-009` High | `R-INVENTORY-002`; CommitReservation has no active caller. | Oracle Decision required; lifecycle control Partial/Indirect. | After lifecycle design, Confirmed→fulfilled/committed decreases OnHand and Reserved exactly once; cancellation before/after; aged recovery. | New cross-service contract/service likely; XL; future Release; architecture first. |
| `GAP-010` High | `R-RESILIENCE-002`; only Orders outbox broker restart. | Recovery objective time Decision required; publish control Partial. | Stop broker with unacked/pending work, restart twice; consumers resume, topology exists, exactly-once logical effects, all publishers resume, bounded shutdown. | Rabbit controls; M; Nightly; deterministic reset fixture first. |
| `GAP-011` High | `R-DATA-001`; fresh schema only; Catalog omitted from pending check. | Supported baseline/window Decision required; migration control Partial; `GATE-DATA-001/003`. | Each service prior committed populated schema→current; preserve data/constraints/queries; old/new window; Catalog drift. | Versioned DB artifacts; L; Release; snapshot retention first. |
| `GAP-012` High | `R-DEPLOY-001`; built nginx/API networking unproved. | Ingress Oracle Decision required; ingress control Missing; `GATE-DEP-001/002`. | Built frontend/backend behind intended ingress; auth/catalog/basket/checkout; assert network target/health and fail-fast bad base. | Container browser/full routing/Keycloak; L; Main smoke+Release; ingress contract first. |
| `GAP-013` High | `R-DEPLOY-002`; backend host-run, not full container stack. | Production topology Decision required; artifact control Partial; `GATE-DEP-001`. | Every built backend target on internal network; non-root, readiness, gateway, delayed dependency, restart, persisted volume, bounded safe cleanup. | Isolated Compose project; L; Main+Release. |
| `GAP-014` High | `R-BASKET-001/003`; Orders fake-client clear failure now proves committed-order replay, but lost updates, Redis restart and a real Redis/network clear failure remain absent. | Checkout repeat oracle Approved; basket mutation/Redis recovery oracles Decision required; controls Missing/Partial. | Concurrent update under approved merge/conflict; restart with/without persistence; real clear timeout after order commit with retry/idempotency and user-outcome assertions. | Redis/cross-service; M; Nightly; mutation/recovery policies first. |
| `GAP-015` Medium | `R-MSG-002/003`; production topology not exhaustively asserted; option ignored. | Fixed/configured oracle Decision required; DLQ control Partial. | Inspect production declarations: exchange, 15 queues, quorum/DLX/delivery args, bindings/routing; vary config or assert fixed invariant. | Rabbit topology; S; PR/Main; ownership decision first. |
| `GAP-016` Medium | `R-OBS-001`; flows only incidentally execute tracing and some services omit extension. | Diagnostic fields partly Decision required; trace control Partial/Indirect. | Test OTLP collector checkout: gateway/HTTP/message parentage, names, IDs, failure, redaction, ProblemDetails/log linkage. | Collector/log sink; L; Nightly+Release; consistent registration first. |
| `GAP-017` Medium | `R-FRONTEND-001`; pages/forms/loading/polling untested. | Core oracle Approved; session details Decision required; frontend control Partial. | Testing Library: quantities, disabled/loading, ProblemDetails, email, repeat submit, terminal/nonterminal polling, timeout/unmount using fake timers. | Frontend component/MSW; S/M; PR; page harness first. |
| `GAP-018` Medium | `R-FRONTEND-001`; Chromium only, CI retry 1, no a11y/other engines. | Support matrix Decision required; `GATE-ACC-001`, `GATE-COMP-001` target. | Zero-retry repeat job; critical read-only Firefox/WebKit; keyboard/focus/axe; serialized unique payment mutations. | Browser; M; Nightly; deterministic seed/diagnostics first. |
| `GAP-019` Medium | `R-GW-001`; remote IP behind proxy/multiple replicas unknown. | Ingress trust/distributed-limit Decision required; rate-limit control Partial. | Intended proxy + two gateways; trusted versus spoofed forwarded headers, independent subjects, attacker sharing/reset and documented replica behavior. | Security/performance ingress; L; Release; design first. |
| `GAP-020` Closed — TECH-03/TECH-04 accepted | The four existing `ESHOP-DATA-004` selectors assert canonical ProblemDetails fields, trace/request correlation, zero Catalog/Orders persistence and retained basket state. TECH-04 additionally locks Catalog model validation to `application/problem+json`; PR `CI #45`, Main `CI #46` and TestRail R72 passed with 22 Backend Integration aggregates. | Negative atomic-rejection oracle Approved below; controls and risk scores remain unchanged because this strengthens evidence rather than the product price/migration controls. | No remaining GAP-020 or TECH-04 implementation action. Retain the assertions; consider direct media-type coverage for other shared-error-handling consumers separately. | Existing API fixtures; complete; Main. |
| `GAP-021` Medium | `R-MSG-001/R-INVENTORY-001`; direct DB multiline atomicity is covered by TECH-01, but late/reordered broker delivery is absent. | Late-event oracle Decision required; inbox/concurrency controls Partial. | PaymentAuthorized before StockReserved; late failure after terminal; broker-delivered mixed lines; correct ack/DLQ, terminal immutability and no extra outbox. | Messaging; M; Nightly; late policy first. |
| `GAP-022` Closed — PR/Main cutover accepted | All 193 selectors have a fail-closed primary classification (`PR=77`, `Main=97`, `Nightly=19`) and Release overlap (`13`). PR `CI #37` passed Quality policy, Backend and Frontend while Containers, E2E and TestRail were skipped. Main `CI #38` passed the cumulative runtime and published closed TestRail `R55`–`R58` with `12/22/3/4` Passed results. | Governance approved; direct-push-safe cumulative semantics satisfy the groomed contract below; no product control or gate state was changed. | No remaining GAP-022 implementation action. Monitor runtime/cardinality drift and preserve the fail-closed policy checks. | CI/TestRail workflow; complete. |
| `GAP-023` Medium | `R-IDENTITY-001/R-DEPLOY-001`; token negatives and production origin/header matrix are absent. | Token/session and security-header/origin policies partly Decision required; `GATE-SEC-003`. | Table tokens: absent/expired/nbf/issuer/audience/signature/sub/roles; origin allow/deny; CSP/HSTS/nosniff/frame at production ingress. | Security integration; M; PR tokens + Release headers/TLS. |
| `GAP-024` Low | Test infrastructure uses delays/eventual polling/CI retry. | Determinism standard Approved; potential `DEV-*` only if unavoidable. | Observable readiness/queue/DB state; injected time; preserve first-attempt logs/trace/video; retry occurrence remains failure signal. | Infrastructure; S/M; PR/Main before Nightly expansion. |
| `GAP-025` Low | .NET net10, Node CI24/local22, Chromium only; no support matrix. | Oracle Decision required. | Approve runtime/browser/OS versions; targeted Windows/Linux builds if supported, engine smoke and architecture check. | CI/platform; M; Nightly/Release after policy. |
| `GAP-026` High — TECH-05 locally complete; shared acceptance residual | `R-GW-AUTH-001`; the authoritative registry covers all 13 YARP proxy routes and 3 local endpoints. One 43-row theory asserts public/authenticated/role behavior and zero downstream requests after denials; the quality validator fails on route, policy, role, method, sample-path or registry drift. | Oracle Approved; `CTRL-SEC-GATEWAY-001` advances to Direct locally for the gateway boundary; `GATE-SEC-001` remains a Future W2 activation prerequisite. | Obtain PR/Main acceptance and the existing `ESHOP-GW-001` TestRail publication, then retain the fail-closed registry. Direct Catalog network isolation remains GAP-003. | Remaining shared acceptance XS; Main; Release gate not activated. |

## Duplication and indirect evidence

- Messaging and Playwright checkout paths are deliberate layered overlap: durable distributed effects versus customer-visible outcome.
- Domain transitions remain on PR even when saga tests repeat terminal states.
- Gateway and direct-service authorization are complementary trust boundaries. TECH-05 closes local gateway route exhaustiveness; direct Catalog mutation isolation remains GAP-003.
- Keep minimal liveness smoke per process; implement dependency readiness once per relevant component without multiplying status-only checks.
- Indirect-only risk evidence: traces (`R-OBS-001`), outbox claims (`R-OUTBOX-002`), inventory fulfillment (`R-INVENTORY-002`), real basket-clear recovery (`R-BASKET-003`), production ingress (`R-DEPLOY-001`).

Weak assertions retained from the audit: five service `HealthAnonymousRequestReturnsOk` rows plus messaging smoke are status-only; browser compensation title implies stock release without inventory assertion. TECH-03/GAP-020 removes the Catalog/Orders status-only negative-mutation weakness. No conditional return, skip, `.only`, disabled or quarantine marker was found.

## CI findings

The accepted baseline narrows runtime by event without narrowing total governed coverage. The local TECH-05 contract has PR 77, Main primary 98 and cumulative Main 175, Nightly 19 and Release overlap 13 selectors. CI #37/#38 accepted the earlier event paths; TECH-05 still requires its own PR/Main acceptance. Missing tests and accumulated scheduled evidence remain distinct concerns.

## GAP-020 / TECH-03 groomed atomic-rejection contract

**User outcome:** invalid Catalog and checkout mutations return actionable, correlatable errors and cannot leave partial business state or silently consume the customer basket.

**Approved oracle:** every covered 400 response identifies the failed request with status, type, title, detail, instance, nonblank `traceId` and `requestId`. Model validation additionally identifies the rejected field and `model_validation_failed`. Rejection is atomic: Catalog product cardinality is unchanged; Orders creates no order, item, history, outbox, idempotency or inbox row; checkout never clears the basket and model validation never calls Basket service.

**In scope:** the four existing selectors bound to `ESHOP-DATA-004`, Orders controller-created ProblemDetails metadata and direct fixture-state assertions. **Out of scope:** new validation rules, price freshness/rounding, migration risk reduction, global ProblemDetails media-type normalization, new TestRail cases, selector/tier changes and gate activation.

**Acceptance criteria:**

1. Catalog invalid create returns the canonical validation envelope, names `Name`, exposes correlation IDs and leaves the PostgreSQL product count unchanged.
2. Empty-basket checkout returns traceable `Checkout failed.` detail, does not clear the basket and leaves all Orders persistence tables empty.
3. Mixed-currency checkout returns traceable deterministic detail, preserves both original basket lines and leaves all Orders persistence tables empty.
4. Invalid-email checkout returns the canonical validation envelope, names `CustomerEmail`, does not call Basket service, retains the basket and leaves all Orders persistence tables empty.
5. Selector names, the four `ESHOP-DATA-004` bindings, PR/Main ownership and locked TestRail `12/22/3/4` report counts remain unchanged.
6. Local targeted Catalog `1/1` and Orders `3/3` pass before shared Main/TestRail acceptance. The observed Catalog `application/json` media type is an adjacent transport-standardization finding, not silently folded into this atomic-rejection ticket.

## GAP-022 groomed PR/Main cutover contract

**User outcome:** pull requests receive fast deterministic feedback without Docker, browser or TestRail secret use; a direct push or dispatch on `main` still executes every PR and Main selector before publishing evidence.

**In scope:** event conditions, generated positive filters for mixed Main/Nightly .NET projects, fail-closed selector/report cardinality, artifact and TestRail behavior, direct-push safety and rollback evidence. **Out of scope:** changing selector ownership, activating gates, moving new tests between tiers, GitHub branch protection and reducing compile coverage.

**Acceptance criteria:**

1. `pull_request` runs Quality policy, compiles backend test projects, executes 66 backend-unit rows and 13 frontend rows, and skips Docker-backed integration execution, Container images, Checkout E2E and TestRail publication.
2. `push main` and `workflow_dispatch` execute 77 PR plus 97 Main logical selectors: 66 backend-unit, 96 backend-integration, 13 frontend and 3 E2E executable rows.
3. Inventory, Messaging and Orders Main filters contain exactly 14, 4 and 9 selectors; all Nightly-primary selectors remain excluded from Main.
4. Main TestRail publication creates four closed runs with exactly 12 Backend Unit, 22 Backend Integration, 3 Frontend Unit and 4 Checkout E2E Passed TestIntents; `trcli -n` creates no case.
5. Nightly and Release matrices/counts remain `19 → 11` and `13 → 6`; policy drift, unknown selectors, changed cardinality or missing source groups fail before test execution.
6. Rollback is the cutover commit only; the previous broad PR/main behavior remains recoverable without changing test code or TestRail cases.

## TECH-04 groomed ProblemDetails media-type contract

**User outcome:** Catalog validation failures are machine-readable under the standard ProblemDetails JSON media type rather than a generic JSON response.

**Approved oracle:** the existing invalid-create request returns status 400 and `Content-Type: application/problem+json` while preserving the accepted ValidationProblemDetails body, request correlation and zero-write invariant.

**In scope:** shared invalid-model-state serialization and the existing Catalog selector bound to `ESHOP-DATA-004`. **Out of scope:** new validation rules, exception-response redesign, new selectors or TestRail cases, mapping/tier changes and global assertion coverage for every shared-error-handling consumer.

**Acceptance criteria:**

1. `CreateProductInvalidRequestReturnsBadRequest` fails if the response media type is not exactly `application/problem+json`.
2. Status, canonical body fields, `Name` validation error, correlation IDs and unchanged product cardinality remain asserted.
3. The shared factory uses an explicit JSON result so MVC/API-versioning content negotiation cannot downgrade the declared media type.
4. Targeted Catalog `1/1`, full Catalog `10/10`, Release solution build and governance tooling pass locally.
5. The existing selector name, four `ESHOP-DATA-004` bindings, tier ownership and locked TestRail `12/22/3/4` cardinality do not change.

**Accepted evidence:** PR CI #45 and Main CI #46 passed. Main commit `b298107` published closed TestRail R71–R74 at `12/22/3/4`; `[Negative mutations]` passed in R72. TECH-04 is complete without reopening GAP-020 or changing a gate lifecycle.

## GAP-026 / TECH-05 groomed gateway-authorization contract

**User outcome:** every endpoint addressable through the API Gateway has an explicit, reviewable access policy, and a denied request cannot reach a downstream service.

**Approved oracle:** the gateway configuration and authoritative registry contain the same 13 YARP routes with identical cluster, path, method, authorization and rate-limit metadata, plus the three known local endpoints. Public endpoints accept anonymous requests; authenticated endpoints reject anonymous users; role-protected endpoints reject anonymous and authenticated wrong-role users and accept every configured role. Every denied proxy request leaves the downstream request count at zero; every successful proxy variant forwards exactly once with the representative method and path.

**In scope:** gateway route/configuration drift, `AuthenticatedUser`, `CustomerOnly` and `SupportOrAdmin` behavior, all configured allowed roles, representative public routes, local `/`, `/health` and `/api/v1/auth/me`, and the existing `ESHOP-GW-001` TestRail aggregate. **Out of scope:** downstream resource ownership after forwarding, direct Catalog mutation reachability, production ingress/TLS, distributed rate limiting, new TestRail cases and gate activation.

**Acceptance criteria:**

1. [`gateway-route-policy.json`](../../scripts/quality/gateway-route-policy.json) contains exactly 16 unique addressable entries: 13 proxy routes and 3 local endpoints.
2. [`gateway_routes.py`](../../scripts/quality/gateway_routes.py) fails closed on missing/extra routes or drift in cluster, path, methods, authorization policy, rate limiter, roles or representative request matching.
3. `EveryAddressableRouteEnforcesAuthorizationAndForwarding` executes 43 variants and proves the approved status/role/forwarding oracle for every registry entry.
4. A denied proxy request produces no fake-downstream request; a successful proxy request produces exactly one with the expected method and path.
5. The full gateway suite passes `65/65`; quality unit tests, TestRail tooling tests and tier/map validation pass locally.
6. The selector is Main-owned and binds to existing `ESHOP-GW-001`; the runtime contract becomes 194 selectors/212 edges and cumulative Main 175 without changing locked TestRail report cardinality `12/22/3/4`.
7. PR and Main CI pass, Main republishes `ESHOP-GW-001`, and only then is shared acceptance recorded. `GATE-SEC-001` remains Future and unevaluated.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 2.5 | 2026-07-29 | Groomed and implemented TECH-05/GAP-026 locally with a 16-endpoint registry, 43 authorization variants and fail-closed drift validation; shared acceptance remains pending. | Pending review |
| 2.4 | 2026-07-29 | Accepted TECH-04 through CI #45/#46 and TestRail R72; removed the transport follow-up residual with unchanged cardinality. | Pending review |
| 2.3 | 2026-07-29 | Groomed and implemented TECH-04 locally; retained shared Main/TestRail acceptance as the only transport follow-up residual. | Pending review |
| 2.2 | 2026-07-29 | Closed GAP-020 after PR CI #41 and Main CI #42/TestRail R64 accepted TECH-03 with unchanged cardinality. | Pending review |
| 2.1 | 2026-07-29 | Groomed and implemented TECH-03/GAP-020 locally while retaining shared Main/TestRail acceptance as the only residual. | Pending review |
| 2.0 | 2026-07-29 | Closed GAP-022 after PR CI #37 and Main CI #38/R55–R58 satisfied every groomed acceptance criterion. | Pending review |
| 1.9 | 2026-07-29 | Groomed and implemented cumulative PR/Main cutover locally; retained shared-event acceptance as the only GAP-022 residual. | Pending review |
| 1.8 | 2026-07-29 | Accepted QA-03 Nightly R49 and Release R50; narrowed GAP-022 to the governed PR/Main rollout. | Pending review |
| 1.7 | 2026-07-29 | Promoted GAP-001 through CI #34/TestRail R42 and implemented the QA-03 tier contract/workflow locally. | Pending review |
| 1.6 | 2026-07-28 | Implemented the remaining GAP-001 broker-delivery/no-DLQ variant locally; retained shared CI/TestRail and scheduled-history residuals. | Pending review |
| 1.5 | 2026-07-28 | Promoted GAP-002 workflow variants to shared evidence after CI #33/TestRail R38; only scheduled history remains. | Pending review |
| 1.4 | 2026-07-28 | Marked GAP-002 implementation locally complete after QA-02 exact downstream workflow proof; retained shared CI/TestRail and scheduled-history residuals. | Pending review |
| 1.3 | 2026-07-28 | Promoted GAP-001/GAP-002 named variants to shared evidence after CI #31 and TestRail R30/R31 passed; retained only messaging/workflow and scheduled residuals. | Pending review |
| 1.2 | 2026-07-28 | Updated GAP-002 after TECH-02 approval, implementation, local direct evidence and TestRail synchronization; narrowed GAP-014 residual scope. | Pending review |
| 1.1 | 2026-07-28 | Updated GAP-001 after local TECH-01 proof and linked proposed TECH-02 idempotency oracle. | Pending review |
