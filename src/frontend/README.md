# Eshop Frontend

React frontend for the Eshop capstone microservices project.

The frontend is intentionally small and focused.

It demonstrates usable Catalog and Basket flows through the API Gateway.

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
- Catalog API client
- Basket API client
- navigation with React Router
- API Gateway integration
- Vite development proxy for `/api`
- loading states
- API error states
- add product to basket
- update basket quantity
- remove basket item
- clear basket
- local development identity header
- Docker development target
- production-style nginx target
- Visual Studio `.esproj` project file

Not implemented yet:

- Keycloak login
- JWT access token handling
- product detail page
- checkout flow
- order status page
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
npm install
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

Example:

```text
/api/v1/products
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

Until Keycloak integration is implemented, Basket requests use:

```env
VITE_DEVELOPMENT_CUSTOMER_ID=local-development-user
```

The frontend sends this value through:

```text
X-Customer-Id
```

This mechanism is allowed only for local Development.

It must be replaced by the authenticated JWT `sub` claim later.

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
  → PostgreSQL
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

## Required Local Services

For the complete Catalog and Basket flow, run:

- PostgreSQL
- Redis
- CatalogService
- BasketService
- ApiGateway
- React frontend

Start infrastructure:

```bash
docker compose up -d postgres redis
```

Run CatalogService:

```bash
dotnet run --project src/backend/services/CatalogService/CatalogService.csproj
```

Run BasketService:

```bash
dotnet run --project src/backend/services/BasketService/BasketService.csproj
```

Run ApiGateway:

```bash
dotnet run --project src/backend/gateways/ApiGateway/ApiGateway.csproj
```

Run the frontend:

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

UI hiding is not security.

Authorization must be enforced by API Gateway and downstream services.
