from __future__ import annotations

import copy
import importlib.util
import sys
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "gateway_routes.py"
SPEC = importlib.util.spec_from_file_location("gateway_routes", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class GatewayRoutePolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.registry = MODULE.load_json(MODULE.DEFAULT_REGISTRY)
        cls.appsettings = MODULE.load_json(MODULE.DEFAULT_APPSETTINGS)

    def test_repository_policy_covers_every_route(self) -> None:
        self.assertEqual(
            {"routes": 21, "proxy": 16, "local": 5},
            MODULE.validate(self.registry, self.appsettings),
        )

    def test_missing_governed_proxy_route_fails_closed(self) -> None:
        changed = copy.deepcopy(self.registry)
        changed["routes"] = [
            route
            for route in changed["routes"]
            if route["id"] != "orders-detail-route"
        ]
        with self.assertRaisesRegex(MODULE.GatewayRoutePolicyError, "registry drift"):
            MODULE.validate(changed, self.appsettings)

    def test_authorization_policy_drift_fails_closed(self) -> None:
        changed = copy.deepcopy(self.appsettings)
        changed["ReverseProxy"]["Routes"]["basket-root-route"][
            "AuthorizationPolicy"
        ] = "SupportOrAdmin"
        with self.assertRaisesRegex(
            MODULE.GatewayRoutePolicyError,
            "AuthorizationPolicy drift",
        ):
            MODULE.validate(self.registry, changed)

    def test_duplicate_route_id_fails_closed(self) -> None:
        changed = copy.deepcopy(self.registry)
        changed["routes"].append(copy.deepcopy(changed["routes"][0]))
        with self.assertRaisesRegex(MODULE.GatewayRoutePolicyError, "Duplicate"):
            MODULE.validate(changed, self.appsettings)

    def test_unknown_role_fails_closed(self) -> None:
        changed = copy.deepcopy(self.registry)
        route = next(
            route for route in changed["routes"] if route["id"] == "basket-root-route"
        )
        route["allowed_roles"] = ["superuser"]
        with self.assertRaisesRegex(MODULE.GatewayRoutePolicyError, "allowed_roles"):
            MODULE.validate(changed, self.appsettings)

    def test_non_matching_sample_path_fails_closed(self) -> None:
        changed = copy.deepcopy(self.registry)
        route = next(
            route for route in changed["routes"] if route["id"] == "catalog-detail-route"
        )
        route["sample_path"] = "/api/v1/orders/42"
        with self.assertRaisesRegex(MODULE.GatewayRoutePolicyError, "does not match"):
            MODULE.validate(changed, self.appsettings)

    def test_path_matches_standard_route_parameter(self) -> None:
        self.assertTrue(
            MODULE.path_matches(
                "/api/v{version}/inventory-items/{id}/stock-adjustments",
                "/api/v1/inventory-items/"
                "00000000-0000-0000-0000-000000000001/"
                "stock-adjustments",
            )
        )

    def test_standard_route_parameter_does_not_match_extra_segment(self) -> None:
        self.assertFalse(
            MODULE.path_matches(
                "/api/v{version}/inventory-items/{id}/stock-adjustments",
                "/api/v1/inventory-items/"
                "00000000-0000-0000-0000-000000000001/"
                "unexpected/stock-adjustments",
            )
        )

    def test_local_endpoint_policy_drift_fails_closed(self) -> None:
        changed = copy.deepcopy(self.registry)
        route = next(
            route for route in changed["routes"] if route["id"] == "gateway-auth-me"
        )
        route["authorization_policy"] = None
        with self.assertRaisesRegex(
            MODULE.GatewayRoutePolicyError,
            "AuthorizationPolicy",
        ):
            MODULE.validate(changed, self.appsettings)


if __name__ == "__main__":
    unittest.main()
