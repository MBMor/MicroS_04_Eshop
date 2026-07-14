# Eshop Frontend

React frontend for the Eshop capstone microservices project.

The frontend is intentionally small and focused.

It demonstrates a usable end-to-end flow through the API Gateway.

## Technology Stack

* React
* TypeScript
* Vite
* Docker
* nginx for production-style static hosting
* Visual Studio JavaScript Project System

## Current Status

The frontend currently contains the first working Catalog integration.

Implemented:

* Vite React TypeScript project
* basic React application
* Product Catalog page
* Catalog API client
* API Gateway base URL configuration
* Vite development proxy for `/api`
* local development script
* Docker development target
* production-style nginx target
* Visual Studio `.esproj` project file

Not implemented yet:

* Keycloak login
* React Router
* product detail page
* basket page
* checkout flow
* order status page
* role-aware UI behavior

## Visual Studio Integration

The frontend is included in the solution through:

* `Eshop.Frontend.esproj`

This file allows Visual Studio to show the React frontend as a project inside:

* `Eshop.slnx`

The project uses:

* `Microsoft.VisualStudio.JavaScript.SDK`

Startup command:

```text
npm run dev
```

The frontend build is intentionally not executed automatically during normal solution build.

This is controlled by:

```xml
<ShouldRunBuildScript>false</ShouldRunBuildScript>
```

Reason:

* backend build should stay fast
* frontend build should be explicit
* CI should run backend and frontend jobs separately

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

The frontend reads the API Gateway base URL from:

```text
VITE_API_BASE_URL
```

Recommended local development value:

```env
VITE_API_BASE_URL=
```

An empty value means that browser requests use same-origin relative URLs.

Example request:

```text
/api/v1/products
```

In Vite development mode, `/api` is proxied to the API Gateway.

The proxy target is configured through:

```text
VITE_DEV_PROXY_TARGET
```

Recommended value when running frontend directly on the host machine:

```env
VITE_DEV_PROXY_TARGET=http://localhost:5080
```

Recommended value when running frontend inside Docker Desktop:

```env
VITE_DEV_PROXY_TARGET=http://host.docker.internal:5080
```

Only variables prefixed with `VITE_` are exposed to the browser.

Do not put secrets into frontend environment variables.

## Catalog Page

The current frontend page loads products from:

```text
GET /api/v1/products
```

The request goes through:

```text
React Frontend -> Vite dev proxy -> API Gateway -> Catalog Service
```

Required local services for the catalog page:

* PostgreSQL
* CatalogService
* ApiGateway
* React frontend

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

## Docker Production-style Build

The Dockerfile contains a production target that builds static files and serves them with nginx.

Build manually:

```bash
docker build --target production -t eshop-frontend:local ./src/frontend
```

Run manually:

```bash
docker run --rm -p 8080:80 eshop-frontend:local
```

Open:

```text
http://localhost:8080
```

## Frontend Boundary Rules

The frontend must call only the API Gateway.

Allowed:

```text
React Frontend -> API Gateway
```

Not allowed:

```text
React Frontend -> Catalog Service directly
React Frontend -> Basket Service directly
React Frontend -> Orders Service directly
React Frontend -> PostgreSQL
React Frontend -> Redis
React Frontend -> RabbitMQ
```

## Security Notes

Do not store secrets in React.

Do not expose Keycloak admin credentials in frontend code.

Do not log JWT tokens.

Do not log passwords.

UI hiding is not security.

Authorization must be enforced by API Gateway and downstream services.
