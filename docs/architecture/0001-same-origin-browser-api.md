# ADR 0001: Use Same-Origin Browser API Access

## Status

Accepted.

## Context

The React frontend communicates with the Eshop API Gateway.

During local development, the browser sends relative `/api` requests to the Vite development server. Vite forwards these requests to the API Gateway through its development proxy.

The intended production deployment uses the same model: the frontend and public API are exposed through one public origin, with `/api` requests forwarded to the API Gateway by the edge proxy.

Allowing arbitrary browser origins would expand the set of websites capable of sending API requests from browser JavaScript.

## Decision

The Eshop does not enable CORS in the API Gateway.

The Gateway does not register or invoke ASP.NET Core CORS middleware.

Frontend API requests use relative URLs:

```text
/api/...
```

The development environment uses the Vite `/api` proxy.

The production environment must expose the frontend and the public API through the same scheme, host and port from the browser's perspective.

## Consequences

No `Access-Control-Allow-Origin` response header is returned for foreign origins.

Cross-origin browser requests are blocked by the browser's same-origin policy.

Non-browser clients such as command-line tools and service clients are unaffected by browser CORS enforcement and must still authenticate normally.

Keycloak authentication redirects are separate OpenID Connect navigation flows and do not require enabling CORS for the API Gateway.

## Future change

If the frontend and API are deployed on different origins, this decision must be revisited.

A future CORS configuration must:

- use an explicit origin allowlist;
- allow only required methods;
- allow only required request headers;
- never combine wildcard origins with credentials;
- include automated preflight tests;
- be configured independently for each environment.

`AllowAnyOrigin` must not be used for authenticated production APIs.