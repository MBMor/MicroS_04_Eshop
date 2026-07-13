# Eshop Frontend

React frontend for the Eshop capstone microservices project.

The frontend is intentionally small and focused.

It will demonstrate a usable end-to-end flow through the API Gateway.

## Technology Stack

* React
* TypeScript
* Vite
* Docker
* nginx for production-style static hosting

## Current Status

The frontend currently contains only the initial application skeleton.

Implemented:

* Vite React TypeScript project
* basic App component
* API Gateway base URL configuration
* local development script
* Docker development target
* production-style nginx target

Not implemented yet:

* Keycloak login
* React Router
* product catalog page
* basket page
* checkout flow
* order status page
* API client
* role-aware UI behavior

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

Local default:

```text
http://localhost:5080
```

Example `.env` file:

```env
VITE_API_BASE_URL=http://localhost:5080
```

Only variables prefixed with `VITE_` are exposed to the browser.

Do not put secrets into frontend environment variables.

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
