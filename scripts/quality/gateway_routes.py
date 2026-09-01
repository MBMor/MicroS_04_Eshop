#!/usr/bin/env python3
"""Validate the governed API Gateway route and authorization contract."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_REGISTRY = Path(__file__).with_name("gateway-route-policy.json")
DEFAULT_APPSETTINGS = (
    REPOSITORY_ROOT / "src" / "backend" / "gateways" / "ApiGateway" / "appsettings.json"
)
KNOWN_ROLES = {"customer", "support", "admin"}
POLICY_ROLES = {
    None: set(),
    "AuthenticatedUser": set(),
    "CustomerOnly": {"customer"},
    "SupportOrAdmin": {"support", "admin"},
    "AdminOnly": {"admin"},
}
LOCAL_ROUTES = {
    "gateway-root": {
        "path_template": "/",
        "methods": ["GET"],
        "authorization_policy": None,
        "allowed_roles": [],
        "rate_limiter_policy": None,
    },
    "gateway-health": {
        "path_template": "/health",
        "methods": ["GET"],
        "authorization_policy": None,
        "allowed_roles": [],
        "rate_limiter_policy": None,
    },
    "gateway-liveness": {
        "path_template": "/live",
        "methods": ["GET"],
        "authorization_policy": None,
        "allowed_roles": [],
        "rate_limiter_policy": None,
    },
    "gateway-readiness": {
        "path_template": "/ready",
        "methods": ["GET"],
        "authorization_policy": None,
        "allowed_roles": [],
        "rate_limiter_policy": None,
    },
    "gateway-auth-me": {
        "path_template": "/api/v1/auth/me",
        "methods": ["GET"],
        "authorization_policy": "AuthenticatedUser",
        "allowed_roles": [],
        "rate_limiter_policy": None,
    },
    "gateway-operational-health": {
        "path_template": "/api/v1/operations/health",
        "methods": ["GET"],
        "authorization_policy": "SupportOrAdmin",
        "allowed_roles": ["support", "admin"],
        "rate_limiter_policy": "Operational",
    },
}


class GatewayRoutePolicyError(ValueError):
    pass


def load_json(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as stream:
        value = json.load(stream)

    if not isinstance(value, dict):
        raise GatewayRoutePolicyError(f"Expected a JSON object in {path}.")

    return value


def validate(
    registry: dict[str, Any],
    appsettings: dict[str, Any],
) -> dict[str, int]:
    if registry.get("version") != 1:
        raise GatewayRoutePolicyError("Gateway route policy version must be 1.")

    governed_routes = registry.get("routes")
    if not isinstance(governed_routes, list) or not governed_routes:
        raise GatewayRoutePolicyError("Gateway route policy needs a non-empty routes list.")

    proxy = appsettings.get("ReverseProxy")
    configured_routes = proxy.get("Routes") if isinstance(proxy, dict) else None
    configured_clusters = proxy.get("Clusters") if isinstance(proxy, dict) else None
    if not isinstance(configured_routes, dict) or not isinstance(configured_clusters, dict):
        raise GatewayRoutePolicyError(
            "ApiGateway appsettings must define ReverseProxy Routes and Clusters."
        )

    seen_ids: set[str] = set()
    governed_proxy_ids: set[str] = set()
    governed_local_ids: set[str] = set()

    for route in governed_routes:
        if not isinstance(route, dict):
            raise GatewayRoutePolicyError("Every governed route must be an object.")

        route_id = required_string(route, "id")
        if route_id in seen_ids:
            raise GatewayRoutePolicyError(f"Duplicate governed route id {route_id!r}.")
        seen_ids.add(route_id)

        kind = required_string(route, "kind")
        if kind not in {"local", "proxy"}:
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} has unsupported kind {kind!r}."
            )

        path_template = required_string(route, "path_template")
        sample_method = required_string(route, "sample_method").upper()
        sample_path = required_string(route, "sample_path")
        methods = optional_methods(route, route_id)
        if methods is not None and sample_method not in methods:
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} sample method {sample_method!r} is not configured."
            )
        if not path_matches(path_template, sample_path):
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} sample path {sample_path!r} does not match "
                f"{path_template!r}."
            )

        policy = route.get("authorization_policy")
        if policy not in POLICY_ROLES:
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} has unknown authorization policy {policy!r}."
            )

        roles = route.get("allowed_roles")
        if not isinstance(roles, list) or not all(
            isinstance(role, str) and role in KNOWN_ROLES for role in roles
        ):
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} has invalid allowed_roles."
            )
        if len(roles) != len(set(roles)) or set(roles) != POLICY_ROLES[policy]:
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} roles do not match policy {policy!r}."
            )

        if kind == "local":
            governed_local_ids.add(route_id)
            if route.get("cluster_id") is not None:
                raise GatewayRoutePolicyError(
                    f"Local route {route_id!r} cannot reference a cluster."
                )
            expected_local = LOCAL_ROUTES.get(route_id)
            if expected_local is None:
                raise GatewayRoutePolicyError(
                    f"Unknown local endpoint {route_id!r}."
                )
            compare(route_id, "Path", expected_local["path_template"], path_template)
            compare(route_id, "Methods", expected_local["methods"], methods)
            compare(
                route_id,
                "AuthorizationPolicy",
                expected_local["authorization_policy"],
                policy,
            )
            compare(route_id, "AllowedRoles", expected_local["allowed_roles"], roles)
            compare(
                route_id,
                "RateLimiterPolicy",
                expected_local["rate_limiter_policy"],
                route.get("rate_limiter_policy"),
            )
            continue

        governed_proxy_ids.add(route_id)
        configured = configured_routes.get(route_id)
        if not isinstance(configured, dict):
            raise GatewayRoutePolicyError(
                f"Governed proxy route {route_id!r} is missing from appsettings."
            )

        cluster_id = required_string(route, "cluster_id")
        if cluster_id not in configured_clusters:
            raise GatewayRoutePolicyError(
                f"Route {route_id!r} references unknown cluster {cluster_id!r}."
            )

        match = configured.get("Match")
        if not isinstance(match, dict):
            raise GatewayRoutePolicyError(
                f"Configured route {route_id!r} is missing Match."
            )

        compare(route_id, "ClusterId", cluster_id, configured.get("ClusterId"))
        compare(route_id, "Path", path_template, match.get("Path"))
        compare(route_id, "Methods", methods, configured_methods(match, route_id))
        compare(
            route_id,
            "AuthorizationPolicy",
            policy,
            configured.get("AuthorizationPolicy"),
        )
        compare(
            route_id,
            "RateLimiterPolicy",
            route.get("rate_limiter_policy"),
            configured.get("RateLimiterPolicy"),
        )

    configured_ids = set(configured_routes)
    if governed_proxy_ids != configured_ids:
        raise GatewayRoutePolicyError(
            "Proxy route registry drift: missing="
            f"{sorted(configured_ids - governed_proxy_ids)}, extra="
            f"{sorted(governed_proxy_ids - configured_ids)}."
        )
    local_route_ids = set(LOCAL_ROUTES)
    if governed_local_ids != local_route_ids:
        raise GatewayRoutePolicyError(
            "Local endpoint registry drift: missing="
            f"{sorted(local_route_ids - governed_local_ids)}, extra="
            f"{sorted(governed_local_ids - local_route_ids)}."
        )

    return {
        "routes": len(governed_routes),
        "proxy": len(governed_proxy_ids),
        "local": len(governed_local_ids),
    }


def required_string(value: dict[str, Any], field: str) -> str:
    result = value.get(field)
    if not isinstance(result, str) or not result.strip():
        raise GatewayRoutePolicyError(f"Field {field!r} must be a non-empty string.")
    return result


def optional_methods(route: dict[str, Any], route_id: str) -> list[str] | None:
    methods = route.get("methods")
    if methods is None:
        return None
    if not isinstance(methods, list) or not methods or not all(
        isinstance(method, str) and method == method.upper() and method.isalpha()
        for method in methods
    ):
        raise GatewayRoutePolicyError(f"Route {route_id!r} has invalid methods.")
    if len(methods) != len(set(methods)):
        raise GatewayRoutePolicyError(f"Route {route_id!r} has duplicate methods.")
    return methods


def configured_methods(match: dict[str, Any], route_id: str) -> list[str] | None:
    methods = match.get("Methods")
    if methods is None:
        return None
    if not isinstance(methods, list) or not all(isinstance(method, str) for method in methods):
        raise GatewayRoutePolicyError(
            f"Configured route {route_id!r} has invalid Methods."
        )
    return methods


def path_matches(template: str, sample: str) -> bool:
    pattern = re.escape(template)

    pattern = pattern.replace(
        r"\{\*\*catch\-all\}",
        r".+",
    )

    pattern = re.sub(
        r"\\\{[^{}]+\\\}",
        r"[^/]+",
        pattern,
    )

    return re.fullmatch(pattern, sample) is not None


def compare(route_id: str, field: str, expected: Any, actual: Any) -> None:
    if expected != actual:
        raise GatewayRoutePolicyError(
            f"Route {route_id!r} {field} drift: expected {expected!r}, got {actual!r}."
        )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate",))
    parser.add_argument("--registry", type=Path, default=DEFAULT_REGISTRY)
    parser.add_argument("--appsettings", type=Path, default=DEFAULT_APPSETTINGS)
    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        summary = validate(
            load_json(arguments.registry),
            load_json(arguments.appsettings),
        )
    except (OSError, json.JSONDecodeError, GatewayRoutePolicyError) as error:
        print(f"Gateway route policy invalid: {error}")
        return 1

    print(
        "Gateway route policy valid: "
        f"routes={summary['routes']}, proxy={summary['proxy']}, local={summary['local']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
