from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "test_tiers.py"
SPEC = importlib.util.spec_from_file_location("test_tiers", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class TestTierPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.policy = MODULE.load_json(MODULE.DEFAULT_POLICY)
        cls.automation_map = MODULE.load_json(MODULE.DEFAULT_AUTOMATION_MAP)
        cls.selectors = MODULE.automation_selectors(cls.automation_map)
        cls.classified = MODULE.classify(cls.policy, cls.selectors)

    def test_every_automation_selector_is_classified_once(self) -> None:
        self.assertEqual(193, len(self.classified))
        self.assertEqual(
            self.selectors,
            {item.selector for item in self.classified},
        )

    def test_expected_primary_and_release_counts(self) -> None:
        primary = {
            tier: sum(item.primary_tier == tier for item in self.classified)
            for tier in MODULE.PRIMARY_TIERS
        }
        self.assertEqual({"PR": 77, "Main": 97, "Nightly": 19}, primary)
        self.assertEqual(13, sum(item.release for item in self.classified))

    def test_expected_testrail_aggregate_and_edge_counts(self) -> None:
        self.assertEqual(
            {"Nightly": (11, 25), "Release": (6, 21)},
            MODULE.validate_aggregate_counts(self.policy, self.automation_map),
        )

    def test_nightly_matrix_contains_three_dotnet_projects(self) -> None:
        matrix = MODULE.build_matrix(self.classified, "nightly")
        self.assertEqual(
            {
                "inventory-service": 3,
                "messaging-integration": 9,
                "orders-service": 7,
            },
            {item["name"]: item["logical_tests"] for item in matrix["include"]},
        )

    def test_release_matrix_contains_approved_overlap(self) -> None:
        matrix = MODULE.build_matrix(self.classified, "release")
        self.assertEqual(
            {
                "inventory-service": 3,
                "messaging-integration": 3,
                "orders-service": 7,
            },
            {item["name"]: item["logical_tests"] for item in matrix["include"]},
        )

    def test_unknown_override_fails_closed(self) -> None:
        changed = json.loads(json.dumps(self.policy))
        changed["nightly_selectors"].append("MissingTests.Unknown")
        with self.assertRaisesRegex(MODULE.PolicyError, "unknown selectors"):
            MODULE.classify(changed, self.selectors)


if __name__ == "__main__":
    unittest.main()
