# Eshop Operations Console

The Eshop Operations Console is a Windows desktop application for support and administrative troubleshooting of the Microservices Eshop system.

It complements the customer-facing React frontend.

The React application represents the customer experience, while the Operations Console provides authenticated access to operational business state, audit information, cross-service navigation, diagnostics, and investigation workflows.

## Purpose

The Operations Console is designed for scenarios such as:

- inspecting an order reported by a customer
- locating the payment associated with an order
- inspecting inventory for an ordered product
- reviewing customer notifications
- investigating notification correlation metadata
- reviewing stock-adjustment history
- performing controlled administrative stock adjustments
- following related entities across service boundaries
- checking the current operational health of backend services
- investigating degraded or unavailable service dependencies
- handing off from business state to OpenTelemetry traces in Aspire
- inspecting local application and environment diagnostics

The application does not orchestrate backend workflows.

All business state is read from backend services through the API Gateway, and write authorization remains enforced by the API Gateway and owning backend service.

## Technology

The desktop application uses:

- .NET 10
- WPF
- C#
- CommunityToolkit.Mvvm
- Generic Host
- Microsoft dependency injection
- Microsoft configuration
- structured logging
- HttpClientFactory
- OpenID Connect
- Authorization Code Flow with PKCE
- Duende IdentityModel OIDC client
- xUnit

Target framework:

```text
net10.0-windows
```

The production application project is:

```text
src/desktop/Eshop.Operations.Desktop
```

Desktop tests are located in:

```text
tests/desktop/Eshop.Operations.Desktop.Tests
```

## Architecture

```text
Eshop Operations Console
        |
        | HTTPS / HTTP
        v
API Gateway
        |
        +--> Catalog Service
        |
        +--> Orders Service
        |
        +--> Inventory Service
        |
        +--> Payments Service
        |
        +--> Notifications Service

Authentication
        |
        v
Keycloak

Observability hand-off
        |
        v
Aspire Dashboard
```

The desktop application does not call backend services directly.

Application API traffic goes through the API Gateway.

Authentication communicates with Keycloak using the native-application OIDC flow.

## Authentication

The Operations Console uses:

```text
Authorization Code Flow
+
PKCE S256
+
system browser
+
loopback redirect
```

The desktop OIDC client is a public native client.

No client secret is stored in the application.

Access and refresh tokens are kept in memory.

The application does not persist tokens to a local database.

The application can start anonymously.

Catalog remains available without operational authentication.

Protected operational modules require a user with an appropriate role.

## Roles

The primary local roles are:

| Role | Desktop access |
| --- | --- |
| `customer` | No operational access |
| `support` | Read-only operational access |
| `admin` | Operational access plus approved administrative mutations |

Support and admin users can access protected operational screens.

Inventory stock adjustment is intentionally restricted to the `admin` role.

UI role checks are a usability feature only.

The authoritative security boundaries remain:

```text
API Gateway
+
backend service authorization
```

Knowing an operational endpoint URL does not bypass authentication or authorization.

## Application sections

### Catalog

Catalog is the anonymous read-only entry point.

Capabilities include:

- product list
- search
- category filtering
- sorting
- product selection
- product detail inspection
- copy-friendly detail fields

Catalog uses the anonymous API Gateway HTTP client.

### Investigate

Investigate provides a central entry point when an operator already knows a business identifier.

Supported lookup types include:

```text
Order
Payments for order
Notifications for order
Inventory for product
```

The operator enters the complete GUID and chooses the lookup type.

The application then navigates to the relevant operational screen and applies the corresponding troubleshooting context.

Exact Order lookup uses the dedicated operational order-detail endpoint.

It does not page through the complete Orders list to locate one order.

### Orders

Orders provides read-only cross-customer operational order inspection for support and admin users.

Capabilities include:

- bounded server paging
- Load more
- client-side search over loaded summaries
- status filtering
- sorting
- lazy detail loading
- customer identity inspection
- order items
- payment method
- status history
- copyable operational values

The list intentionally loads lightweight order summaries.

Full Order details are requested only after selecting or directly investigating an Order.

This avoids N+1 detail requests during list loading.

### Inventory

Inventory provides operational stock inspection.

Capabilities include:

- SKU and Product ID search
- sorting
- stock quantities
- item selection
- stock-adjustment history
- adjustment audit detail
- Operation ID
- Trace ID
- actor identity
- reason
- before/after quantities
- version information

Support users have read-only access.

Admin users can additionally perform stock adjustments.

### Safe stock adjustment workflow

Administrative stock adjustment uses multiple safety mechanisms:

```text
explicit confirmation
+
mandatory reason
+
expected version
+
optimistic concurrency
+
idempotency key
+
immutable audit record
```

The desktop never performs optimistic local stock mutation.

The server response is authoritative.

If the client loses the HTTP response after sending the request, the outcome can be unknown.

In that case, the application retains the original operation request and idempotency key and allows retrying the exact same operation.

A new stock operation is blocked until the unknown outcome is resolved.

### Stock adjustment history

Adjustment history is loaded explicitly for the selected inventory item.

It uses bounded paging.

The history exposes operational audit information including:

- Operation ID
- Inventory item
- delta
- expected version
- result version
- reason
- actor subject
- actor username
- outcome
- error
- before quantities
- after quantities
- Trace ID
- occurrence time

The idempotency key is intentionally not exposed through the read-only history contract.

### Payments

Payments provides read-only operational payment inspection.

Capabilities include:

- search
- Order ID filtering
- sorting
- payment status
- payment method
- failure information
- customer identity
- timestamps
- navigation back to the related Order

When Orders opens Payments for one Order ID and exactly one matching payment exists, the matching payment is selected automatically.

### Notifications

Notifications provides cross-customer operational notification inspection for support and admin users.

Capabilities include:

- bounded paging
- Order ID filtering
- Customer ID filtering
- Correlation ID filtering
- notification detail
- audit metadata
- contextual navigation from Order to Notifications
- contextual navigation from Notification to Order

When an Order-focused lookup returns exactly one notification, the matching notification can be selected automatically.

### Diagnostics

Diagnostics combines three related but intentionally separate troubleshooting surfaces:

1. local application and environment information
2. Operational Health
3. observability hand-off to Aspire

#### Local diagnostics

The screen exposes local application/runtime information including:

* environment
* API Gateway base address
* API timeout
* application version
* build information
* .NET runtime
* operating system
* process architecture
* Aspire Dashboard URL

The values are copy-friendly so they can be included in support notes or troubleshooting reports.

#### Operational Health

Operational Health provides a point-in-time view of the backend services reachable through the API Gateway.

A support or admin user can explicitly refresh the health snapshot.

The screen shows:

* overall platform status
* check timestamp
* individual service status
* probe duration
* failure information
* failed dependency information
* downstream HTTP status where available

Each service row also provides an `Investigate` action so that a degraded service can become the starting point for further troubleshooting.

Operational Health answers the question:

> Which backend service is unhealthy right now?

It does not perform continuous monitoring and does not replace alerting or distributed tracing.

#### Observability

When an Aspire Dashboard URL is configured, Diagnostics provides:

`Open Aspire dashboard`

Aspire is used for deeper investigation of:

* distributed traces
* HTTP service calls
* RabbitMQ message processing
* errors
* durations
* business correlation metadata

Aspire answers a different question:

> What happened during this specific request or distributed business operation?

The Operations Console intentionally does not query OpenTelemetry telemetry directly.

## Cross-service troubleshooting

The desktop provides contextual navigation between related business entities.

Examples:

```text
Order
  -> Payments

Order
  -> Notifications

Order item
  -> Inventory

Payment
  -> Order

Notification
  -> Order
```

Contextual navigation records why the operator arrived at the destination.

Example:

```text
Context: Order 29ae3072… -> Payments
```

The full business identifier remains available in the target view.

The context can be cleared explicitly.

Normal navigation also clears contextual filters so stale investigation state is not silently preserved.

## Business identifiers

The most important operational identifiers are:

### Order ID

Primary identifier for one checkout workflow.

Used by:

- Orders
- Payments
- Notifications
- Aspire business tags

OpenTelemetry tag:

```text
eshop.order.id
```

### Product ID

Used to connect an Order item to Inventory.

### Payment ID

Identifies one Payment entity.

OpenTelemetry tag:

```text
eshop.payment.id
```

### Correlation ID

Used for asynchronous messaging correlation.

OpenTelemetry/business tag:

```text
eshop.correlation_id
```

### Trace ID

Identifies one OpenTelemetry distributed execution trace.

A Trace ID is not the same as an Order ID.

Multiple later HTTP requests for the same Order normally produce different Trace IDs.

## Operations Console vs Aspire

The two tools have intentionally different responsibilities.

```text
Operations Console
    business state
    entity identifiers
    operational actions
    audit history
    cross-service navigation
    current operational health
    service-level failure diagnostics

Aspire Dashboard
    traces
    spans
    distributed service dependencies
    messaging execution
    durations
    errors
    telemetry attributes
```

A useful distinction is:

`Operational Health -> What is unhealthy right now?`

`Aspire -> What happened during this specific operation?`

The recommended troubleshooting workflow is:

```text
Operations Console
        |
        | obtain Order ID / Payment ID / Correlation ID
        v
Aspire Dashboard
        |
        | inspect distributed execution
        v
Operations Console
        |
        | verify resulting business state
        v
Resolution
```

See:

```text
docs/operations/observability-troubleshooting.md
```

## Operational Health

The API Gateway exposes a protected operational health aggregation endpoint:

`GET /api/v1/operations/health`

Required access:

`support` or `admin`

The endpoint probes the health endpoints of:

* Catalog
* Basket
* Orders
* Inventory
* Payments
* Notifications

Each downstream probe is bounded by a timeout so that one unavailable service cannot block the health request indefinitely.

The aggregate response contains:

* overall status
* check timestamp
* per-service status
* per-service response duration
* failure diagnostics where applicable
* failed dependency information where applicable
* downstream HTTP status where available

The overall status is `Healthy` when every monitored downstream service reports healthy.

If one or more services cannot report healthy, the aggregate status is `Degraded`.

### Operations Console workflow

Operational Health is exposed directly through the Diagnostics screen:

    Operations Console
        -> Diagnostics
        -> Operational health
        -> Refresh health

The desktop retrieves the aggregate through the API Gateway and displays both the overall status and individual service results.

For a service that requires attention, the operator can use:

`Investigate`

to continue into the relevant troubleshooting context.

The desktop also handles common operational failure states explicitly, including:

* missing authentication
* expired authentication session
* insufficient role
* request timeout
* unavailable API Gateway
* unexpected request failure

Operational Health is intentionally a point-in-time diagnostic check.

It is useful for answering:

> Which service is unhealthy right now?

It is not intended to replace:

* continuous infrastructure monitoring
* alerting
* trace storage
* distributed tracing

For investigation of a specific distributed operation, use the Aspire Dashboard.

## Configuration

Desktop configuration is stored in:

```text
src/desktop/Eshop.Operations.Desktop/appsettings.json
```

Important sections include:

```text
Desktop
ApiGateway
Authentication
Observability
```

Typical configuration includes:

```text
Desktop:EnvironmentName
ApiGateway:BaseAddress
ApiGateway:TimeoutSeconds
Authentication:Authority
Authentication:ClientId
Authentication:Scopes
Observability:DashboardUrl
```

Configuration is validated during application startup.

Invalid required configuration prevents normal application startup.

## Local development

Start the required infrastructure:

```bash
docker compose up -d \
  postgres \
  redis \
  rabbitmq \
  keycloak \
  aspire-dashboard
```

Start the backend services from Visual Studio or individually.

Then run the desktop project:

```bash
dotnet run \
  --project src/desktop/Eshop.Operations.Desktop/Eshop.Operations.Desktop.csproj
```

The API Gateway must be reachable through the configured `ApiGateway:BaseAddress`.

## Local test users

The repository contains development-only Keycloak users.

Refer to:

```text
docs/identity.md
infrastructure/keycloak/README.md
```

for the authoritative local identity setup.

Do not reuse development credentials outside local or automated-test environments.

## Desktop tests

Run:

```bash
dotnet build \
  tests/desktop/Eshop.Operations.Desktop.Tests/Eshop.Operations.Desktop.Tests.csproj \
  --configuration Release \
  --no-restore \
  -warnaserror
```

Then:

```bash
dotnet test \
  tests/desktop/Eshop.Operations.Desktop.Tests/Eshop.Operations.Desktop.Tests.csproj \
  --configuration Release \
  --no-build
```

The desktop test suite covers areas such as:

- API client contracts
- authentication state
- token lifecycle
- protected navigation
- list filtering and sorting
- server paging
- Orders detail loading
- Inventory adjustment safety
- unknown write outcome retry
- adjustment history
- cross-service contextual navigation
- Notifications operational filtering
- investigation lookup
- local Diagnostics behavior
- Operational Health API contracts
- Operational Health refresh and cancellation behavior
- Operational Health authentication and authorization failures
- Operational Health connectivity and timeout handling
- service-level investigation navigation

## CI and publishing

The Windows desktop job runs on a Windows GitHub Actions runner.

The CI pipeline:

- restores desktop dependencies
- builds with warnings treated as errors
- runs desktop tests
- publishes a `win-x64` application
- validates the published application startup
- generates SHA-256 checksums
- verifies build provenance
- uploads the desktop artifact

Published artifact:

```text
eshop-operations-desktop-win-x64
```

The application supports startup validation through:

```text
--validate-startup
```

which allows CI to verify configuration and host startup without running the interactive UI.

## Local development data

The repository contains deterministic development-data tooling for reproducing the local environment on another machine.

Development snapshots are stored under:

```text
infrastructure/dev-data
```

and helper scripts under:

```text
scripts/dev
```

These files contain development/test data only.

They must never be treated as a production backup strategy.

## Security boundaries

The Operations Console follows these principles:

1. All business API calls go through the API Gateway.
2. Protected backend services independently validate bearer tokens.
3. Support/admin visibility in WPF is not considered a security boundary.
4. Stock mutation remains protected server-side by admin authorization.
5. Actor identity is derived from validated JWT claims.
6. Tokens are not written to a local database.
7. Idempotency protects retried administrative mutations.
8. Operational read APIs are explicitly separated from customer-owned APIs.

## Known boundaries

The current desktop intentionally does not implement:

- direct backend-service access
- its own local business database
- offline operation
- customer-facing checkout
- trace storage
- direct OpenTelemetry querying
- distributed workflow orchestration
- automatic repair actions
- production alert management

These responsibilities remain with the owning services or dedicated observability infrastructure.

## Related documentation

- `README.md`
- `docs/identity.md`
- `docs/operations/observability-troubleshooting.md`
- `infrastructure/keycloak/README.md`
- `docs/testing/test-strategy.md`
- `docs/testing/testrail-ci-integration.md`
- `docs/testing/quality-gate-policy.md`
