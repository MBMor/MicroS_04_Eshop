# Eshop Frontend

React frontend for the Eshop capstone microservices project.

The frontend is intentionally small and focused.

It currently demonstrates usable Catalog, Basket, Checkout, and Orders flows through the API Gateway.

## Technology Stack

- React
- TypeScript
- Vite
- React Router
- Docker
- nginx for production-style static hosting
- Visual Studio JavaScript Project System

## Current Status

Implemented:

- Vite React TypeScript project
- Product Catalog page
- Basket page
- Checkout page
- Orders list page
- Order details and status page
- Catalog API client
- Basket API client
- Orders API client
- navigation with React Router
- API Gateway integration
- Vite development proxy for `/api`
- loading states
- API error states
- add product to basket
- update basket quantity
- remove basket item
- clear basket
- create durable order from basket
- display order status
- short order-status polling
- manual order-status refresh
- local development identity header
- Docker development target
- production-style nginx target
- Visual Studio `.esproj` project file

Not implemented yet:

- Keycloak login
- JWT access token handling
- product detail page
- stock reservation flow
- payment processing flow
- notification display
- role-aware UI behavior

## Visual Studio Integration

The frontend is included in the solution through:

- `Eshop.Frontend.esproj`

This allows Visual Studio to display the React frontend as a project inside:

- `Eshop.slnx`

Startup command:

```text
npm run dev
```

The frontend build is intentionally not executed automatically during normal solution build.

This is controlled by:

```xml
<ShouldRunBuildScript>false</ShouldRunBuildScript>
```

Backend and frontend builds should remain separate in CI.

## Local Development

Install dependencies:

```bash
npm ci --no-audit --no-fund
```

Run the development server:

```bash
npm run dev
```

Open:

```text
http://localhost:5173
```

## Type Check

```bash
npm run typecheck
```

## Lint

```bash
npm run lint
```

## Build

```bash
npm run build
```

## Preview Production Build

```bash
npm run preview
```

## Environment Configuration

### API base URL

The frontend reads the API Gateway base URL from:

```text
VITE_API_BASE_URL
```

Recommended development value:

```env
VITE_API_BASE_URL=
```

An empty value means browser requests use relative URLs.

Examples:

```text
/api/v1/products
/api/v1/basket
/api/v1/orders
```

### Vite proxy

In development, `/api` requests are proxied to the API Gateway.

Host development value:

```env
VITE_DEV_PROXY_TARGET=http://localhost:5080
```

Docker Desktop frontend container value:

```env
VITE_DEV_PROXY_TARGET=http://host.docker.internal:5080
```

### Temporary development identity

Until Keycloak integration is implemented, Basket and Orders requests use:

```env
VITE_DEVELOPMENT_CUSTOMER_ID=local-development-user
```

The frontend sends this value through:

```text
X-Customer-Id
```

This mechanism is allowed only for local Development.

It must later be replaced by the authenticated JWT `sub` claim.

Do not use it as a production authentication mechanism.

## Product Catalog

The Product Catalog loads products from:

```text
GET /api/v1/products
```

Request flow:

```text
React
  → API Gateway
  → CatalogService
  → PostgreSQL / catalog_db
```

The page supports:

- loading products
- displaying product metadata
- displaying product prices
- adding active products to the basket
- loading and error states

## Basket

The Basket page uses:

```text
GET    /api/v1/basket
POST   /api/v1/basket/items
PUT    /api/v1/basket/items/{productId}
DELETE /api/v1/basket/items/{productId}
DELETE /api/v1/basket
```

Request flow:

```text
React
  → API Gateway
  → BasketService
  → Redis
```

When adding a product:

```text
BasketService
  → CatalogService
```

BasketService retrieves the authoritative product name, display price, currency, and active state from CatalogService.

The frontend does not send product prices as trusted values.

## Checkout

The Checkout page creates an order through:

```text
POST /api/v1/orders
```

The frontend sends only:

- customer email
- selected fake payment method

The frontend does not send order items or trusted prices.

Server-side flow:

```text
React
  → API Gateway
  → OrdersService
  → BasketService
  → Redis
```

OrdersService loads the current basket directly from BasketService.

OrdersService then:

1. validates that the basket is not empty
2. validates that all items use one currency
3. creates a durable order
4. stores the order in PostgreSQL
5. attempts to clear the basket

## Orders

Orders endpoints:

```text
POST /api/v1/orders
GET  /api/v1/orders
GET  /api/v1/orders/{id}
```

Request flow:

```text
React
  → API Gateway
  → OrdersService
  → PostgreSQL / orders_db
```

New orders start in:

```text
PendingStockReservation
```

The order may temporarily remain in this status because the system is eventually consistent.

RabbitMQ, outbox, Inventory processing, and Payments processing are not implemented yet.

The Order Details page:

- displays the current order status
- displays order item snapshots
- displays the order total
- polls briefly for status changes
- allows manual status refresh
- explains pending eventual-consistency states

## Required Local Services

For the current frontend flow, run:

- PostgreSQL
- Redis
- CatalogService
- BasketService
- OrdersService
- ApiGateway
- React frontend

Start infrastructure:

```bash
docker compose up -d postgres redis
```

Apply Catalog migration:

```bash
dotnet ef database update \
  --project src/backend/services/CatalogService/CatalogService.csproj \
  --startup-project src/backend/services/CatalogService/CatalogService.csproj
```

Apply Orders migration:

```bash
dotnet ef database update \
  --project src/backend/services/OrdersService/OrdersService.csproj \
  --startup-project src/backend/services/OrdersService/OrdersService.csproj
```

Run CatalogService:

```bash
dotnet run --project src/backend/services/CatalogService/CatalogService.csproj
```

Run BasketService:

```bash
dotnet run --project src/backend/services/BasketService/BasketService.csproj
```

Run OrdersService:

```bash
dotnet run --project src/backend/services/OrdersService/OrdersService.csproj
```

Run ApiGateway:

```bash
dotnet run --project src/backend/gateways/ApiGateway/ApiGateway.csproj
```

Run frontend:

```bash
cd src/frontend
npm run dev
```

## Docker Development

From the repository root:

```bash
docker compose build frontend
docker compose up -d frontend
```

Open:

```text
http://localhost:5173
```

The backend services are still expected to run on the host machine during this phase.

## Docker Production-style Build

Build:

```bash
docker build --target production -t eshop-frontend:local ./src/frontend
```

Run:

```bash
docker run --rm -p 8080:80 eshop-frontend:local
```

Open:

```text
http://localhost:8080
```

## Frontend Boundary Rules

Allowed:

```text
React Frontend → API Gateway
```

Not allowed:

```text
React Frontend → CatalogService directly
React Frontend → BasketService directly
React Frontend → OrdersService directly
React Frontend → PostgreSQL
React Frontend → Redis
React Frontend → RabbitMQ
```

## Security Notes

Do not store secrets in React.

Do not expose Keycloak admin credentials in frontend code.

Do not log JWT tokens.

Do not log passwords.

Variables prefixed with `VITE_` are exposed to browser code and must never contain secrets.

The temporary `X-Customer-Id` development header is not authentication.

UI hiding is not security.

Authorization must be enforced by API Gateway and downstream services.
