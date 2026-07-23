# Identity and Access Management

This document describes authentication, authorization, customer ownership and local Keycloak operation in the Eshop system.

## Overview

The Eshop uses Keycloak as its OpenID Connect identity provider.

The authentication architecture consists of:

- Keycloak realm `eshop`
- React SPA client `eshop-frontend`
- API audience `eshop-api`
- API Gateway JWT validation
- downstream service JWT validation
- realm-role authorization
- customer ownership derived from the JWT `sub` claim
- Authorization Code Flow with PKCE in the React frontend

The frontend authenticates users directly with Keycloak. It then sends the access token to the API Gateway using the HTTP `Authorization` header.

```text
Browser
  |
  | Authorization Code Flow with PKCE
  v
Keycloak
  |
  | access token
  v
React frontend
  |
  | Authorization: Bearer <access-token>
  v
API Gateway
  |
  | validates token and authorization policy
  v
Backend service
  |
  | validates token again
  v
Application operation
```

## Trust Boundaries

### Keycloak

Keycloak is responsible for:

- user authentication
- password verification
- token issuance
- token signing
- application role assignment
- OpenID Connect discovery
- login and logout sessions

### React frontend

The React frontend is an untrusted public client.

It is responsible for:

- starting the login redirect
- completing Authorization Code Flow with PKCE
- keeping tokens in memory
- refreshing access tokens
- attaching access tokens to API requests
- showing role-aware navigation

The frontend is not responsible for enforcing security.

Hiding a navigation item or rendering an access-denied page is only a user-experience feature. The API Gateway and backend services enforce the actual authorization rules.

### API Gateway

The API Gateway is responsible for:

- validating access-token signatures
- validating the token issuer
- validating the `eshop-api` audience
- validating token expiration
- requiring authentication on protected routes
- applying route-level role policies
- forwarding authorized requests to backend services

### Backend services

Protected backend services also validate JWT access tokens independently.

This prevents a caller from bypassing the API Gateway and directly invoking an exposed service port without a valid token.

## Local Keycloak URLs

Keycloak:

```text
http://localhost:18080
```

Keycloak Admin Console:

```text
http://localhost:18080/admin/
```

Eshop Account Console:

```text
http://localhost:18080/realms/eshop/account/
```

OpenID Connect discovery document:

```text
http://localhost:18080/realms/eshop/.well-known/openid-configuration
```

## Realm

Application realm:

```text
eshop
```

The application does not use the Keycloak `master` realm.

The `master` realm is reserved for Keycloak administration.

## Clients

### `eshop-frontend`

`eshop-frontend` is a public OpenID Connect client used by the React SPA.

Configuration:

- public client
- no client secret
- Authorization Code Flow enabled
- PKCE required with `S256`
- implicit flow disabled
- direct access grants disabled
- service account disabled
- access-token audience includes `eshop-api`

Local redirect URIs:

```text
http://localhost:5173/
http://localhost:5173/silent-check-sso.html
```

Local web origin:

```text
http://localhost:5173
```

Local post-logout redirect URI:

```text
http://localhost:5173/
```

A client secret must never be added to the React frontend. Browser applications cannot protect confidential credentials.

### `eshop-api`

`eshop-api` represents the protected API audience.

Configuration:

- bearer-only client
- no browser login
- no direct access grants
- no service account
- no interactive authentication flow

Access tokens accepted by the Eshop APIs must contain:

```text
aud: eshop-api
```

## Local Users

The imported development realm contains the following users.

| Username | Password | Realm role |
|---|---|---|
| `alice.customer` | `Alice123!` | `customer` |
| `sam.support` | `Support123!` | `support` |
| `anna.admin` | `Admin123!` | `admin` |

These accounts and passwords are intended only for local development.

They must not be used in production or shared environments.

## Application Roles

### `customer`

Customer-facing access.

Typical operations:

- view and modify the authenticated customer's basket
- create an order
- list the authenticated customer's orders
- read the authenticated customer's order details
- read the authenticated customer's notifications

### `support`

Operational support access.

Typical operations:

- inspect inventory
- inspect payment operations

The support role does not grant access to another customer's basket or customer-owned orders.

### `admin`

Application administration access.

Typical operations:

- inspect inventory
- inspect payment operations
- perform future administrative operations

The application `admin` role does not grant access to the Keycloak Admin Console.

Keycloak administration requires separate `realm-management` permissions in the `master` realm.

## Authorization Policies

Shared policy names are defined in `Security.Shared`.

| Policy | Required identity |
|---|---|
| `AuthenticatedUser` | any authenticated user |
| `CustomerOnly` | role `customer` |
| `SupportOnly` | role `support` |
| `AdminOnly` | role `admin` |
| `SupportOrAdmin` | role `support` or `admin` |
| `CustomerOrAdmin` | role `customer` or `admin` |

## Gateway Authorization Matrix

| Route | Access |
|---|---|
| `/` | anonymous |
| `/health` | anonymous |
| `/api/v1/products` | anonymous |
| `/api/v1/products/{...}` | anonymous |
| `/api/v1/auth/me` | authenticated user |
| `/api/v1/basket` | `customer` |
| `/api/v1/basket/{...}` | `customer` |
| `/api/v1/orders` | `customer` |
| `/api/v1/orders/{...}` | `customer` |
| `/api/v1/notifications` | `customer` |
| `/api/v1/notifications/{...}` | `customer` |
| `/api/v1/inventory-items` | `support` or `admin` |
| `/api/v1/inventory-items/{...}` | `support` or `admin` |
| `/api/v1/payments` | `support` or `admin` |
| `/api/v1/payments/{...}` | `support` or `admin` |

An unauthenticated request to a protected endpoint returns:

```text
401 Unauthorized
```

An authenticated request without the required role returns:

```text
403 Forbidden
```

## Token Claims

The system keeps the original JWT claim names by disabling inbound claim mapping.

Important claims include:

| Claim | Purpose |
|---|---|
| `sub` | stable user identifier and customer ownership key |
| `preferred_username` | display and diagnostic username |
| `email` | user email |
| `roles` | flattened application realm roles |
| `realm_access.roles` | standard Keycloak realm-role structure |
| `aud` | intended token audience |
| `iss` | token issuer |
| `exp` | token expiration |

The backend uses:

```text
sub
```

as the customer identifier.

It does not use the username or email as the ownership key.

## Customer Ownership

Basket, Orders and Notifications are customer-owned resources.

Ownership is derived exclusively from the validated token subject:

```text
JWT sub claim
```

For example:

```text
sub = 2f3ad5ca-8ba8-44ba-9789-b0289870ca74
```

This value becomes the customer ID used by the application.

The system does not trust a customer ID supplied by the browser in:

- request bodies
- query parameters
- route parameters
- custom customer headers

The previous development header:

```text
X-Customer-Id
```

is not a production authentication mechanism.

Sending only this header to a protected service must not authenticate the caller.

## Direct-Service Protection

Protected backend services validate the bearer token even when called directly.

For example, this request must return `401 Unauthorized`:

```bash
curl -i \
  -H "X-Customer-Id: victim-user" \
  http://localhost:5082/api/v1/basket
```

A valid access token is required:

```bash
curl -i \
  -H "Authorization: Bearer $CUSTOMER_TOKEN" \
  http://localhost:5082/api/v1/basket
```

If both a valid token and a forged customer header are supplied, ownership is determined from the token's `sub` claim.

## Orders-to-Basket Token Relay

Creating an order requires Orders Service to read and later clear the authenticated customer's basket.

Orders Service therefore forwards the original bearer token when calling Basket Service:

```text
Browser
  |
  | Bearer token
  v
API Gateway
  |
  | Bearer token
  v
Orders Service
  |
  | same Bearer token
  v
Basket Service
```

Basket Service validates the forwarded token independently and derives the basket owner from the token subject.

Orders Service verifies that the customer ID used by the order operation matches the authenticated `sub` claim before performing the downstream request.

## React Authentication

The React frontend uses:

```text
keycloak-js 26.2.4
```

The Keycloak JavaScript adapter has its own release cycle and does not need to match the Keycloak server patch version.

Authentication is initialized before React Router so the adapter can process the OpenID Connect callback safely.

The frontend uses:

```text
Authorization Code Flow with PKCE S256
```

It does not use:

- implicit flow
- password grant
- a client secret
- manual password collection
- token exchange through the frontend server

## Token Storage

The Keycloak JavaScript adapter keeps access and refresh tokens in memory.

The application must not persist tokens in:

- Local Storage
- Session Storage
- IndexedDB
- cookies managed by frontend JavaScript

Session Storage may contain only the temporary return path:

```text
eshop.auth.return-path
```

This value is used to restore the application route after login.

It does not contain authentication credentials.

## Access-Token Refresh

Before an API request, the frontend attempts to refresh the access token when it is close to expiration.

The API client then adds:

```http
Authorization: Bearer <access-token>
```

If refresh fails:

- the Keycloak token state is cleared
- the API request fails
- the user must authenticate again

Concurrent refresh requests share the same in-flight refresh operation to avoid multiple simultaneous refresh calls.

## Silent SSO

The frontend uses:

```text
/silent-check-sso.html
```

for silent authentication-state checks.

The page sends the callback URL to the parent application using `postMessage`.

The URI must be registered as an allowed redirect URI for `eshop-frontend`.

## Frontend Role Behaviour

### Anonymous user

An anonymous user can:

- browse the product catalog
- open public product routes
- start the sign-in flow

Basket and Orders navigation is hidden.

Opening a protected route displays a sign-in-required state.

### Customer

A user with the `customer` role can:

- add products to the basket
- view and modify the basket
- perform checkout
- list orders
- view order details

### Support

A user with the `support` role:

- cannot use customer-owned basket and order pages
- can call support-authorized operational APIs
- receives `403 Forbidden` from customer-only APIs

### Admin

A user with the `admin` role:

- can call administrative operational APIs
- does not automatically gain customer ownership
- receives `403 Forbidden` from customer-only APIs unless also assigned the `customer` role

## Backend Configuration

Protected .NET applications use the following configuration section:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:18080/realms/eshop",
    "Audience": "eshop-api",
    "RequireHttpsMetadata": false
  }
}
```

Local development uses HTTP and therefore sets:

```text
RequireHttpsMetadata = false
```

Production environments must use HTTPS and set:

```text
RequireHttpsMetadata = true
```

The production `Authority` value must be explicitly configured.

A protected service must fail during startup when required Keycloak configuration is missing or invalid.

## Frontend Configuration

The frontend uses the following public environment variables:

```dotenv
VITE_KEYCLOAK_URL=http://localhost:18080
VITE_KEYCLOAK_REALM=eshop
VITE_KEYCLOAK_CLIENT_ID=eshop-frontend
```

API configuration:

```dotenv
VITE_API_BASE_URL=
VITE_DEV_PROXY_TARGET=http://localhost:5080
```

An empty `VITE_API_BASE_URL` means that the frontend uses same-origin `/api` requests and the Vite development proxy forwards them to the API Gateway.

All `VITE_*` values are embedded in browser JavaScript.

They must not contain:

- passwords
- access tokens
- refresh tokens
- client secrets
- administrator credentials

## Starting the Local Identity Infrastructure

Validate Docker Compose:

```bash
docker compose config --quiet
```

Start PostgreSQL and Keycloak:

```bash
docker compose up -d postgres keycloak
```

Check Keycloak health:

```bash
docker inspect \
  --format='{{.State.Health.Status}}' \
  eshop-keycloak
```

Expected result:

```text
healthy
```

Verify OpenID Connect discovery:

```bash
curl --fail \
  --silent \
  --show-error \
  http://localhost:18080/realms/eshop/.well-known/openid-configuration \
  > /dev/null
```

## Realm Import

The realm definition is stored in:

```text
infrastructure/keycloak/eshop-realm.json
```

Docker Compose mounts it into:

```text
/opt/keycloak/data/import/eshop-realm.json
```

Keycloak starts with:

```text
start-dev --import-realm
```

The startup import creates the `eshop` realm when it does not already exist.

It does not update or overwrite an existing realm.

## Reimporting the Realm

Authenticate the Keycloak administration CLI:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh config credentials \
  --config /tmp/eshop-kcadm.config \
  --server http://localhost:8080 \
  --realm master \
  --user admin \
  --password admin_password
```

Delete only the application realm:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh delete realms/eshop \
  --config /tmp/eshop-kcadm.config
```

Restart Keycloak:

```bash
docker compose restart keycloak
```

The missing realm will be recreated from the import file.

Do not use the following command only to refresh the realm:

```bash
docker compose down -v
```

The PostgreSQL volume is shared with other local service databases. Removing all volumes deletes the complete local application state.

## Verifying the Imported Realm

Authenticate `kcadm` first, then verify the realm:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get realms/eshop \
  --config /tmp/eshop-kcadm.config \
  --fields realm,enabled,displayName
```

Verify roles:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get roles \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --fields name
```

Verify users:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get users \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --fields username,enabled,email
```

Verify the frontend client:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get clients \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --query clientId=eshop-frontend \
  --fields clientId,publicClient,standardFlowEnabled,directAccessGrantsEnabled
```

Verify the API client:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get clients \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --query clientId=eshop-api \
  --fields clientId,bearerOnly,standardFlowEnabled,directAccessGrantsEnabled
```

For target-realm operations, use:

```text
-r eshop
```

The long `--realm master` option in the login command identifies the realm where the administrator authenticates.

## Running the Application

Start the required infrastructure:

```bash
docker compose up -d \
  postgres \
  redis \
  rabbitmq \
  keycloak
```

Run the backend services and API Gateway from Visual Studio or the command line.

Run the frontend locally:

```bash
cd src/frontend
npm ci --no-audit --no-fund
npm run dev
```

Open:

```text
http://localhost:5173
```

## Manual Verification

### Anonymous catalog access

```bash
curl -i \
  http://localhost:5080/api/v1/products
```

The response must not be `401 Unauthorized`.

### Protected route without token

```bash
curl -i \
  http://localhost:5080/api/v1/basket
```

Expected result:

```text
401 Unauthorized
```

### Invalid token

```bash
curl -i \
  -H "Authorization: Bearer invalid-token" \
  http://localhost:5080/api/v1/auth/me
```

Expected result:

```text
401 Unauthorized
```

### Authenticated user endpoint

With a valid token:

```bash
curl --fail \
  --silent \
  --show-error \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  http://localhost:5080/api/v1/auth/me
```

The response includes:

- subject
- preferred username
- email
- application roles

### Customer access

A customer token must be accepted by:

```text
/api/v1/basket
/api/v1/orders
/api/v1/notifications
```

A customer token must receive `403 Forbidden` from:

```text
/api/v1/inventory-items
/api/v1/payments
```

### Support access

A support token must be accepted by:

```text
/api/v1/inventory-items
/api/v1/payments
```

A support token must receive `403 Forbidden` from:

```text
/api/v1/basket
/api/v1/orders
/api/v1/notifications
```

## Automated Tests

### API Gateway integration tests

These tests verify:

- anonymous routes
- `401 Unauthorized`
- `403 Forbidden`
- customer role access
- support role access
- admin role access
- `/api/v1/auth/me`
- authorized YARP forwarding

Run:

```bash
dotnet test \
  tests/backend/integration/ApiGateway.IntegrationTests/ApiGateway.IntegrationTests.csproj
```

The tests use an in-process authentication handler and fake downstream server.

They do not require a running Keycloak instance.

### Messaging integration tests

These tests use an isolated test authentication scheme so messaging tests do not depend on a live identity provider.

Run:

```bash
dotnet test \
  tests/backend/integration/Eshop.Messaging.IntegrationTests/Eshop.Messaging.IntegrationTests.csproj
```

### Frontend authentication tests

Frontend tests verify:

- anonymous route-guard state
- role-denied state
- authorized rendering
- login initiation
- bearer-token attachment
- `401` and `403` handling
- `204 No Content` handling

Run:

```bash
cd src/frontend

npm ci --no-audit --no-fund
npm run typecheck
npm run lint
npm run test
npm run build
```

## CI Quality Gates

The CI pipeline should validate:

### Backend

- restore
- Release build
- API Gateway integration tests
- messaging integration tests
- publication of test-result artifacts

### Frontend

- deterministic `npm ci`
- TypeScript type checking
- ESLint
- Vitest
- production build

Identity changes should not be merged when any of these checks fail.

## Troubleshooting

### `Configuration value 'Keycloak:Authority' is required`

The protected application reads Keycloak configuration during service registration.

Ensure that the configuration contains:

```text
Keycloak:Authority
Keycloak:Audience
Keycloak:RequireHttpsMetadata
```

Integration tests that replace JWT authentication must still provide syntactically valid Keycloak configuration before the application entry point runs.

### Valid token returns `401 Unauthorized`

Check:

- token issuer matches `Keycloak:Authority`
- token audience contains `eshop-api`
- token is not expired
- Keycloak signing keys are reachable
- the service uses the same realm as the token
- HTTP metadata is allowed only in local development

Inspect token claims without logging the complete token.

### Authenticated user receives `403 Forbidden`

Authentication succeeded, but the token does not contain the role required by the endpoint policy.

Check:

```text
roles
realm_access.roles
```

Also verify that the role was assigned in the `eshop` realm and that the frontend client's role mapper is present.

### Realm JSON changes are ignored

Startup import does not overwrite an existing realm.

Delete only the `eshop` realm and restart Keycloak.

### Git Bash rewrites `/opt/keycloak/...`

Git Bash may convert a Linux container path to a Windows path.

Use:

```bash
MSYS_NO_PATHCONV=1 docker exec ...
```

Alternatively, for the current shell:

```bash
export MSYS_NO_PATHCONV=1
```

### Visual Studio cannot resolve `keycloak-js`

The dependency may exist only in the Docker `node_modules` volume.

Install the lockfile dependencies on the Windows host:

```bash
cd src/frontend
npm ci --no-audit --no-fund
```

Then verify:

```bash
npm ls keycloak-js
```

Expected dependency:

```text
keycloak-js@26.2.4
```

Restart the Visual Studio TypeScript language service if stale diagnostics remain.

### Login redirect is rejected

Verify that the exact callback URL is registered for `eshop-frontend`.

Expected local redirect URIs:

```text
http://localhost:5173/
http://localhost:5173/silent-check-sso.html
```

Do not add broad wildcard redirect URIs unless the deployment architecture requires them.

### Login succeeds but frontend appears anonymous

Check:

- browser console for Keycloak adapter errors
- network requests to the token endpoint
- `silent-check-sso.html` availability
- third-party cookie restrictions
- token claims and role mapping
- that authentication initialization completes before React Router starts

### API request contains no bearer token

Check:

- Keycloak reports an authenticated session
- the access token exists in adapter memory
- token refresh succeeded
- the request uses the shared `apiRequest` function
- no direct `fetch` call bypasses the API client

## Production Hardening

Before production deployment:

- run Keycloak in production mode
- use HTTPS for Keycloak, frontend and APIs
- set `RequireHttpsMetadata` to `true`
- replace local development passwords
- use external secret management
- configure a production database for Keycloak
- configure Keycloak hostname and proxy settings
- restrict redirect URIs and web origins
- configure session and token lifetimes
- configure brute-force protection
- configure account recovery according to product requirements
- configure email verification where required
- enable monitoring and alerting
- back up the Keycloak database
- test signing-key rotation
- define an administrative access model
- avoid exposing backend service ports publicly
- consider a BFF architecture if browser-side token handling becomes unsuitable for the application's risk profile

## Security Invariants

The following invariants must remain true:

1. The React client has no client secret.
2. The frontend does not persist access or refresh tokens.
3. Protected routes require a valid access token.
4. Tokens must be issued by the configured `eshop` realm.
5. Tokens must contain the `eshop-api` audience.
6. Customer ownership comes from the validated `sub` claim.
7. Client-provided customer headers are not trusted.
8. Backend services validate tokens independently.
9. Role-aware UI does not replace server-side authorization.
10. Local development credentials are never used in production.