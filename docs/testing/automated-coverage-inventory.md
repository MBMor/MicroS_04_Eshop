# Automated Coverage Inventory

> **Document type:** Point-in-time executable-test inventory  
> **Version:** 1.0  
> **Effective from:** 2026-07-26 audit baseline  
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Baseline:** `main` / `bf3d1afbd7bc6bbfdb7ab8994ca3ad36e51e643c`  
> **Analysis date:** 2026-07-26 (Europe/Prague)  
> **Initial working tree:** Clean

One row is one xUnit method, Vitest `it`, or Playwright `test`. A theory is one logical test when rows prove the same risk. There are **177 logical tests and 181 executable cases**; four two-row theories add four executable cases. All 177 are active; none is skipped, disabled, quarantined, conditionally returned or filtered by checked-in CI. Current scheduling is PR and main events, not formal tiers.

The audit environment could discover source tests but had no accessible Docker daemon. Therefore current canonical `Execution status` is **Not run** and `Evidence validity` is **Unknown** for this audit. The legacy row-level assessment was Covered 168 and Partially covered 9; this describes assertion scope, not a release pass. Indirect risk evidence remains separate.

Risk attribution uses the 2.1 taxonomy: `R-IDENTITY-001` for token/session trust; `R-GW-AUTH-001` for gateway and addressable-service authorization; legacy `R-AUTH-001` only for the direct Catalog mutation boundary; and `R-ORDER-SEC-001` for customer order ownership. Counts and executable identities are unchanged.

## Summary and reconciliation

| Project / framework | Logical | Executable | Level | Active/skipped | Recommended tier |
|---|---:|---:|---|---|---|
| `Eshop.Domain.UnitTests` / xUnit v3 | 64 | 66 | Domain/application unit | 64/0 | PR |
| `ApiGateway.IntegrationTests` / xUnit v3 | 22 | 22 | Gateway integration | 22/0 | Main |
| `BasketService.IntegrationTests` / xUnit v3 | 10 | 10 | API/Redis | 10/0 | Main |
| `CatalogService.IntegrationTests` / xUnit v3 | 10 | 10 | API/PostgreSQL | 10/0 | Main |
| `InventoryService.IntegrationTests` / xUnit v3 | 14 | 14 | API/PostgreSQL | 14/0 | Main |
| `OrdersService.IntegrationTests` / xUnit v3 | 9 | 9 | API/PostgreSQL | 9/0 | Main |
| `PaymentsService.IntegrationTests` / xUnit v3 | 12 | 13 | API/PostgreSQL | 12/0 | Main |
| `NotificationsService.IntegrationTests` / xUnit v3 | 13 | 14 | API/PostgreSQL | 13/0 | Main |
| `Eshop.Messaging.IntegrationTests` / xUnit v2 | 10 | 10 | Cross-service messaging | 10/0 | 4 Main; 6 Nightly |
| Frontend / Vitest | 10 | 10 | Component/unit | 10/0 | PR |
| E2E / Playwright | 3 | 3 | Browser workflow | 3/0 | Main |
| **Total** | **177** | **181** | 74 unit/component; 90 API; 10 messaging; 3 browser | **177/0** | **74 PR; 97 Main; 6 Nightly** |

xUnit totals are 164 logical/168 executable, plus 10 Vitest and 3 Playwright. Source inspection found 160 Facts and four Theories. Discovery reconciled these counts.

## Domain and application unit tests (64 logical / 66 executable)

Inherited: `tests/backend/unit/Eshop.Domain.UnitTests`, xUnit v3, in-memory objects/fakes, no external infrastructure, current PR/main, target PR. All are Active and legacy Covered for the named behavior; concurrency/integration limitations are stated.

### Basket application (7)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `AddItemProductNotFoundReturnsNotFound` | R-BASKET-001 | NotFound; no repository write; fake Catalog/repository |
| `AddItemInactiveProductReturnsValidationFailure` | R-BASKET-001 | validation; no write; fake dependency |
| `AddItemActiveProductPersistsUpdatedBasket` | R-BASKET-001 | stored item fields; no concurrency |
| `AddItemInvalidQuantityDoesNotPersistBasket` | R-BASKET-001 | invalid and zero writes |
| `UpdateQuantityMissingItemReturnsNotFound` | R-BASKET-001 | NotFound and no write |
| `RemoveItemLastItemDeletesBasket` | R-BASKET-003 | repository delete; fake Redis |
| `ClearExistingBasketDeletesBasket` | R-BASKET-003 | customer delete; fake Redis |

### Shopping basket domain (12 logical / 13 executable)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `EmptyValidCustomerCreatesEmptyBasket` | R-BASKET-002 | normalized customer/empty items |
| `EmptyBlankCustomerThrows` | R-BASKET-002 | argument exception |
| `TryAddOrIncreaseNewItemAddsItem` | R-BASKET-001 | complete values |
| `TryAddOrIncreaseExistingItemIncreasesQuantity` | R-BASKET-001 | summed one item; sequential |
| `TryAddOrIncreaseNonPositiveQuantityFailsWithoutMutation` (`0`, `-1`) | R-BASKET-001 | two-row Theory; unchanged state |
| `TryAddOrIncreaseQuantityAboveMaximumFails` | R-BASKET-001 | >100 rejected/no mutation |
| `TryUpdateQuantityExistingItemUpdatesQuantity` | R-BASKET-001 | new quantity |
| `TryUpdateQuantityMissingItemFails` | R-BASKET-001 | false/unchanged |
| `TryUpdateQuantityInvalidQuantityFailsWithoutMutation` | R-BASKET-001 | invalid/no mutation |
| `TryRemoveExistingItemRemovesItem` | R-BASKET-001 | item removed |
| `TryRemoveMissingItemFailsWithoutMutation` | R-BASKET-001 | retained item |
| `BasketItemLineTotalMultipliesPriceAndQuantity` | R-ORDER-002 | exact multiplication; no rounding matrix |

### Catalog product domain (6 logical / 7 executable)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `CreateValidDataNormalizesValues` | R-ORDER-002 | normalized product fields/state; functional behavior, not authorization |
| `CreateEmptyIdThrows` | R-ORDER-002 | argument exception; functional behavior, not authorization |
| `CreateNonPositivePriceThrows` (`0`, `-1`) | R-ORDER-002 | two-row Theory |
| `UpdateValidDataUpdatesAndNormalizesValues` | R-ORDER-002 | updated normalized fields |
| `UpdateInvalidSkuThrowsWithoutPartialMutation` | R-ORDER-002 | original values retained |
| `DeactivateActiveProductDeactivatesProduct` | R-ORDER-002 | active false |

### Inventory domain (10)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `CreateValidDataNormalizesSkuAndCalculatesAvailability` | R-INVENTORY-001 | normalized SKU/quantities |
| `TryReserveActiveItemWithAvailableStockReservesQuantity` | R-INVENTORY-001 | reserve/availability; single-threaded |
| `TryReserveInactiveItemReturnsFalseWithoutMutation` | R-INVENTORY-001 | no mutation |
| `TryReserveInsufficientAvailableStockReturnsFalse` | R-INVENTORY-001 | no mutation |
| `UpdateOnHandBelowReservedQuantityThrowsWithoutMutation` | R-INVENTORY-001 | unchanged |
| `AdjustOnHandQuantityResultBelowReservedQuantityThrows` | R-INVENTORY-001 | unchanged |
| `AdjustOnHandQuantityValidIncreaseUpdatesAvailability` | R-INVENTORY-001 | quantities increase |
| `ReleaseReservationReservedStockReturnsItToAvailability` | R-INVENTORY-001 | release arithmetic |
| `CommitReservationReservedStockDecreasesOnHandQuantity` | R-INVENTORY-002 | commit method; unused workflow |
| `ReleaseReservationMoreThanReservedThrowsWithoutMutation` | R-INVENTORY-001 | no mutation |

### Notification domain (9)

| Test | Risk | Verified scope |
|---|---|---|
| `CreateValidDataCreatesUnreadNotification` | R-NOTIFICATION-001 | normalized unread state |
| `CreateEmptyIdThrows` | R-NOTIFICATION-001 | invalid ID |
| `CreateUnsupportedTypeThrows` | R-NOTIFICATION-001 | invalid type |
| `CreateEmptyOptionalOrderIdThrows` | R-NOTIFICATION-001 | invalid optional ID |
| `CreateBlankTitleThrows` | R-NOTIFICATION-001 | blank title |
| `CreateTitleAboveMaximumLengthThrows` | R-NOTIFICATION-001 | title limit |
| `CreateMessageAboveMaximumLengthThrows` | R-NOTIFICATION-001 | message limit |
| `MarkAsReadUnreadNotificationMarksNotificationRead` | R-NOTIFICATION-001 | flag/timestamp |
| `MarkAsReadAlreadyReadNotificationIsIdempotent` | R-MSG-001 | timestamp unchanged |

### Order domain (9)

| Test | Risk | Verified scope |
|---|---|---|
| `CreateValidItemsCreatesPendingOrderAndInitialHistory` | R-ORDER-002 | totals/items/PendingStockReservation/history |
| `CreateItemsUsingDifferentCurrenciesThrows` | R-ORDER-002 | currency invariant |
| `CreateItemBelongingToDifferentOrderThrows` | R-ORDER-002 | ownership invariant |
| `MarkStockReservedPendingOrderMovesToPendingPayment` | R-MSG-001 | transition/history |
| `MarkStockReservationFailedPendingOrderStoresNormalizedReason` | R-MSG-001 | failed state/reason/history |
| `MarkPaymentAuthorizedPendingPaymentConfirmsOrder` | R-PAYMENT-001 | Confirmed/history |
| `PaymentFailureThenCancellationRecordsBothTransitions` | R-PAYMENT-001 | PaymentFailed→Cancelled |
| `MarkPaymentAuthorizedBeforeStockReservationThrowsWithoutMutation` | R-PAYMENT-001 | invalid transition/no mutation |
| `CancelConfirmedOrderThrowsWithoutMutation` | R-PAYMENT-001 | terminal immutability |

### Fake payment processor (4)

| Test | Risk | Verified scope |
|---|---|---|
| `TryProcessSuccessMethodReturnsAuthorizedDecision` | R-PAYMENT-001 | authorized fake decision |
| `TryProcessFailureMethodReturnsFailedDecision` | R-PAYMENT-001 | failed reason |
| `TryProcessUnsupportedMethodReturnsFalse` | R-PAYMENT-001 | unsupported |
| `TryProcessBlankMethodReturnsFalse` | R-PAYMENT-001 | blank |

### Payment domain (7)

| Test | Risk | Verified scope |
|---|---|---|
| `CreatePendingValidDataNormalizesValues` | R-PAYMENT-001 | normalized Pending |
| `CreatePendingNonPositiveAmountThrows` | R-PAYMENT-001 | amount invariant |
| `AuthorizePendingPaymentMarksPaymentAuthorized` | R-PAYMENT-001 | Authorized/timestamp |
| `FailPendingPaymentNormalizesFailureReason` | R-PAYMENT-001 | Failed/reason/timestamp |
| `FailBlankReasonThrowsWithoutMutation` | R-PAYMENT-001 | Pending retained |
| `AuthorizeAlreadyAuthorizedPaymentThrows` | R-PAYMENT-001 | duplicate transition denied |
| `FailAuthorizedPaymentThrowsWithoutMutation` | R-PAYMENT-001 | unchanged Authorized |

## API Gateway integration (22)

Inherited: xUnit v3 in-process gateway, fake downstream/test auth, no containers, current PR/main, target Main.

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `RootAnonymousReturnsOk` | R-DEPLOY-002 | 200/payload; in-process |
| `CatalogAnonymousForwardsRequest` | R-GW-AUTH-001 | public GET forwarded only |
| `AuthMeAnonymousReturnsUnauthorized` | R-IDENTITY-001 | 401 |
| `AuthMeAuthenticatedReturnsClaims` | R-IDENTITY-001 | subject/roles |
| `BasketAnonymousReturnsUnauthorized` | R-GW-AUTH-001 | 401/not forwarded |
| `BasketCustomerForwardsRequest` | R-GW-AUTH-001 | method/path forwarded |
| `OrdersCustomerForwardsRequest` | R-GW-AUTH-001 | method/path |
| `NotificationsCustomerForwardsRequest` | R-GW-AUTH-001 | method/path forwarding; privacy asserted separately |
| `BasketSupportReturnsForbidden` | R-GW-AUTH-001 | 403/not forwarded |
| `OrdersAdminWithoutCustomerRoleReturnsForbidden` | R-GW-AUTH-001 | 403/not forwarded |
| `InventoryCustomerReturnsForbidden` | R-GW-AUTH-001 | 403/not forwarded |
| `InventorySupportForwardsRequest` | R-GW-AUTH-001 | forwards |
| `InventoryAdminForwardsRequest` | R-GW-AUTH-001 | forwards |
| `PaymentsSupportForwardsRequest` | R-GW-AUTH-001 | forwards |
| `PaymentsAdminForwardsRequest` | R-GW-AUTH-001 | forwards |
| `CatalogAnonymousClientExceedsLimitReturnsTooManyRequests` | R-GW-001 | two 200 then 429/Problem/Retry-After; one instance/IP |
| `BasketSameCustomerExceedsLimitReturnsTooManyRequests` | R-GW-001 | subject partition 429 |
| `CheckoutSameCustomerExceedsLimitReturnsTooManyRequests` | R-GW-001 | checkout 429 |
| `CheckoutDifferentCustomersHaveIndependentLimits` | R-GW-001 | independent subjects |
| `OperationalEndpointSameUserExceedsLimitReturnsTooManyRequests` | R-GW-001 | operational 429 |
| `HealthEndpointIsNotRateLimited` | R-RESILIENCE-001 | exemption, not dependency health |
| `CrossOriginPreflightDoesNotGrantCorsAccess` | R-GW-AUTH-001 | no permissive CORS; absence policy only |

## Service API integrations (90 logical / 92 executable)

Inherited: xUnit v3, `WebApplicationFactory`, service fixtures/test auth, Testcontainers PostgreSQL or Redis, current PR/main, target Main. Health rows and three specified negative cases are legacy Partially covered; all others Covered for named variants.

### Basket (10; Redis Testcontainer)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; Redis-down absent |
| `BasketAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `BasketSupportUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `GetBasketNewCustomerReturnsEmptyBasket` | R-BASKET-002 | empty DTO |
| `AddItemActiveProductPersistsInRedisAndIsolatesCustomers` | R-BASKET-002 | persisted and other customer empty; sequential |
| `AddItemUnknownProductReturnsNotFound` | R-BASKET-001 | 404 and empty |
| `AddItemInactiveProductReturnsBadRequest` | R-BASKET-001 | 400/no item |
| `BasketMutationFlowUpdateRemoveAndClearPersistsChanges` | R-BASKET-001 | Redis-visible sequence; no races |
| `RedisRepositorySetAndGetRoundTripsSerializedBasket` | R-BASKET-002 | serialized equivalence |
| `RedisRepositorySetAssignsExpectedAbsoluteExpiration` | R-BASKET-003 | bounded TTL; timing tolerance |

### Catalog (10; PostgreSQL Testcontainer)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `GetProductsDefaultReturnsOnlyActiveProducts` | R-ORDER-002 | inactive excluded |
| `GetProductsIncludeInactiveReturnsAllProducts` | R-ORDER-002 | direct functional API behavior; no authorization assertion |
| `GetProductByIdUnknownProductReturnsNotFound` | R-ORDER-002 | 404 |
| `CreateProductValidRequestPersistsNormalizedProduct` | R-AUTH-001 | 201/location/body/DB; auth unasserted |
| `CreateProductInvalidRequestReturnsBadRequest` | R-ORDER-002 | **Partial:** 400 only; no-write absent |
| `CreateProductDuplicateSkuReturnsConflict` | R-DATA-001 | 409/original retained |
| `UpdateProductValidRequestPersistsNewValues` | R-ORDER-002 | response/DB |
| `UpdateProductDuplicateSkuReturnsConflict` | R-DATA-001 | 409/original values |
| `DeleteProductExistingProductDeactivatesProduct` | R-ORDER-002 | 204/inactive |
| `DeleteProductUnknownProductReturnsNotFound` | R-ORDER-002 | 404 |

### Inventory (14; PostgreSQL Testcontainer)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `InventoryAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `InventoryCustomerUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `CreateInventoryItemSupportUserPersistsNormalizedItem` | R-INVENTORY-001 | 201/body/DB |
| `GetInventoryItemByProductIdReturnsPersistedItem` | R-INVENTORY-001 | matching DTO |
| `GetInventoryItemsDefaultQueryExcludesInactiveItems` | R-INVENTORY-001 | inactive excluded |
| `CreateInventoryItemDuplicateProductIdReturnsConflict` | R-DATA-001 | 409/one row |
| `CreateInventoryItemDuplicateNormalizedSkuReturnsConflict` | R-DATA-001 | 409/one row |
| `UpdateInventoryItemValidRequestPersistsChanges` | R-INVENTORY-001 | response/DB |
| `AdjustInventoryStockValidDeltaPersistsNewQuantity` | R-INVENTORY-001 | quantity persisted |
| `AdjustInventoryStockZeroDeltaReturnsValidationProblem` | R-INVENTORY-001 | validation Problem |
| `AdjustInventoryStockBelowReservedQuantityReturnsBadRequestWithoutMutation` | R-INVENTORY-001 | 400/unchanged |
| `MissingInventoryItemOperationsReturnNotFound` | R-INVENTORY-001 | grouped endpoints 404 |
| `InventoryRowVersionConcurrentUpdatesRejectStaleWrite` | R-INVENTORY-001 | one save/stale conflict; not competing reservation |

### Orders (9; PostgreSQL, fake Basket)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `OrdersAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `OrdersSupportUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `CreateOrderValidBasketPersistsOrderHistoryAndOutbox` | R-ORDER-002, R-OUTBOX-001 | 201/order/items/history/outbox/clear; no idempotency |
| `CreateOrderEmptyBasketReturnsBadRequestWithoutPersistence` | R-ORDER-001 | 400/no order/outbox |
| `CreateOrderMultipleCurrenciesReturnsBadRequest` | R-ORDER-002 | **Partial:** status only, no no-write |
| `GetOrdersReturnsOnlyAuthenticatedCustomersOrders` | R-ORDER-SEC-001 | owner only |
| `GetOrderOtherCustomersOrderReturnsNotFound` | R-ORDER-SEC-001 | other customer receives 404 |
| `CreateOrderInvalidEmailReturnsBadRequest` | R-ORDER-002 | **Partial:** status only, no no-write |

### Payments (12 logical / 13 executable; PostgreSQL)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `PaymentsAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `PaymentsCustomerUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `GetPaymentsOperationalRoleReturnsOk` (`Support`, `Admin`) | R-GW-AUTH-001 | two-row Theory; both 200 |
| `CreatePaymentSuccessMethodPersistsAuthorizedPayment` | R-PAYMENT-001 | 201/DB; implementation emits no event |
| `CreatePaymentFailureMethodPersistsFailedPayment` | R-PAYMENT-001 | 201/failed row/reason |
| `CreatePaymentDuplicateOrderReturnsConflict` | R-PAYMENT-001 | 409/one row; sequential |
| `CreatePaymentUnsupportedMethodReturnsBadRequestWithoutPersistence` | R-PAYMENT-001 | 400/no row |
| `CreatePaymentEmptyOrderIdReturnsValidationProblem` | R-PAYMENT-001 | ProblemDetails |
| `GetPaymentsReturnsPersistedPayments` | R-PAYMENT-001 | list |
| `GetPaymentByIdAndOrderReturnsPersistedPayment` | R-PAYMENT-001 | both query forms |
| `MissingPaymentQueriesReturnNotFound` | R-PAYMENT-001 | both 404 |

### Notifications (13 logical / 14 executable; PostgreSQL)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `NotificationsAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `NotificationsSupportUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `GetNotificationsNewCustomerReturnsEmptyCollection` | R-NOTIFICATION-001 | empty |
| `GetNotificationsReturnsOnlyCurrentCustomerInDescendingOrder` | R-NOTIFICATION-001 | ownership/order |
| `GetNotificationsUnreadOnlyReturnsOnlyUnreadNotifications` | R-NOTIFICATION-001 | unread filter |
| `GetNotificationsOrderFilterReturnsMatchingOrder` | R-NOTIFICATION-001 | order filter |
| `GetNotificationsLimitIsAppliedAfterDescendingOrdering` | R-NOTIFICATION-001 | newest limited IDs |
| `GetNotificationsInvalidLimitReturnsBadRequest` (`0`, `101`) | R-NOTIFICATION-001 | two-row Theory; 400 |
| `GetNotificationsEmptyOrderIdReturnsBadRequest` | R-NOTIFICATION-001 | validation |
| `GetUnreadCountReturnsOnlyCurrentCustomersUnreadCount` | R-NOTIFICATION-001 | exact owner count |
| `GetNotificationByIdOwnerReturnsPersistedNotification` | R-NOTIFICATION-001 | complete owner DTO |
| `GetNotificationByIdOtherCustomerReturnsNotFound` | R-NOTIFICATION-001 | other customer 404 |

## Cross-service messaging integration (10)

Inherited: xUnit v2 serialized `MessagingIntegration`, shared fixture, PostgreSQL/RabbitMQ Testcontainers, real service hosts/topology, HTTP/broker clients and bounded eventual polling. Current PR/main. First four target Main; last six target Nightly. Timing/reset windows are determinism risks.

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `InfrastructureAndServiceHostsAreAvailable` | R-DATA-001, R-DEPLOY-002 | **Partial:** service health/topology/pending migrations; dependency readiness and Catalog migration omitted |
| `CreateOrderHappyPathConfirmsOrder` | R-OUTBOX-001, R-PAYMENT-001 | order/history, stock reserved, payment, notifications, outboxes; fake Basket/no commit |
| `CreateOrderWhenPaymentFailsReleasesStockAndCancelsOrder` | R-PAYMENT-001, R-INVENTORY-001 | compensation durable effects |
| `CreateOrderWithInsufficientStockMarksReservationAsFailed` | R-INVENTORY-001 | failure state/no payment/notifications/outboxes |
| `StockReservationFailedConsumerDuplicateDeliveryAppliesSideEffectsOnce` | R-MSG-001 | one consumer only |
| `StockReservedConsumerInvalidJsonDeadLettersMessageWithoutSideEffects` | R-MSG-002 | sampled queue malformed→DLQ |
| `StockReservedConsumerUnknownOrderDeadLettersMessageWithoutRetry` | R-MSG-002 | permanent prompt DLQ; timing-sensitive |
| `StockReservationFailedConsumerTransientFailureRequeuesAndProcessesMessage` | R-MSG-002 | one exception/consumer |
| `QuorumQueueDeliveryLimitExceededDeadLettersMessage` | R-MSG-002, R-MSG-003 | custom harness, not production queue |
| `OrdersOutboxRabbitMqOutageRetriesAndPublishesAfterRecovery` | R-OUTBOX-001, R-RESILIENCE-002 | Orders publisher only; environment-sensitive |

## Frontend Vitest (10)

Inherited: `src/frontend`, Vitest/jsdom/Testing Library, mocked fetch/auth, no infrastructure, current PR/main, target PR. All active and Covered for named variants.

| Test | Risk | Verified scope |
|---|---|---|
| `apiClient: adds the bearer token to an authenticated request` | R-FRONTEND-001 | Authorization header |
| `apiClient: does not add Authorization when no token exists` | R-FRONTEND-001 | header absent |
| `apiClient: preserves caller headers` | R-FRONTEND-001 | caller + auth headers |
| `apiClient: returns undefined for a 204 response` | R-FRONTEND-001 | undefined |
| `apiClient: uses problem detail for an unauthorized response` | R-FRONTEND-001 | status/message error |
| `apiClient: uses fallback message for a non-JSON forbidden response` | R-FRONTEND-001 | fallback error |
| `RequireRole: shows the sign-in state when unauthenticated` | R-IDENTITY-001, R-FRONTEND-001 | sign-in/child absent |
| `RequireRole: calls login when sign-in clicked` | R-IDENTITY-001, R-FRONTEND-001 | login called |
| `RequireRole: shows access denied when role missing` | R-IDENTITY-001 | denial/child absent |
| `RequireRole: renders children when role present` | R-IDENTITY-001 | protected child |

## Playwright browser scenarios (3)

Inherited: Chromium only, workers 1, serialized, CI retry 1, trace on first retry, screenshots/video on failure, Keycloak login, deterministic seeds; external scripted infrastructure plus seven host backend processes. Current PR/main, target Main.

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `customer completes a successful checkout` | R-ORDER-001, R-PAYMENT-001 | login/basket/submit/Confirmed UI; no inventory/notification persistence |
| `order fails when inventory has insufficient stock` | R-INVENTORY-001 | StockReservationFailed/reason; long polling |
| `failed payment releases stock and cancels the order` | R-PAYMENT-001 | Cancelled/failure UI; inventory release not asserted despite title |

## Cross-cutting conclusions

- Layered overlap across domain, messaging and browser is deliberate.
- Gateway and service authorization protect distinct boundaries; Catalog direct mutation is missing.
- The nine legacy Partially covered rows are six status-only health/migration smoke rows plus Catalog invalid create and two Orders validations.
- Indirect risk evidence: trace propagation, outbox claims, inventory fulfillment, real basket-clear recovery and production ingress.
- Principal timing/flakiness sources: Redis TTL tolerance, fixed reset delays, eventual polling, browser polling, Keycloak login, Testcontainers startup and CI retry 1.
- No existing executable test was found outside current checked-in CI scheduling; no formal Nightly/Release tier exists.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|

