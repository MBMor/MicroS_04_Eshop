from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "test_tiers.py"
CI_WORKFLOW = Path(__file__).parents[3] / ".github" / "workflows" / "ci.yml"
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
        self.assertEqual(4, self.policy["version"])
        self.assertEqual(208, len(self.classified))
        self.assertEqual(
            self.selectors,
            {item.selector for item in self.classified},
        )

    def test_expected_primary_and_release_counts(self) -> None:
        primary = {
            tier: sum(item.primary_tier == tier for item in self.classified)
            for tier in MODULE.PRIMARY_TIERS
        }
        self.assertEqual({"PR": 77, "Main": 112, "Nightly": 19}, primary)
        self.assertEqual(17, sum(item.release for item in self.classified))

    def test_expected_testrail_aggregate_and_edge_counts(self) -> None:
        self.assertEqual(
            {
                "PR": (15, 80),
                "Main": (32, 202),
                "Nightly": (11, 25),
                "Release": (9, 26),
            },
            MODULE.validate_aggregate_counts(self.policy, self.automation_map),
        )

    def test_execution_tiers_are_cumulative_only_for_main(self) -> None:
        actual = {
            tier: len(
                MODULE.execution_selectors(self.classified, self.policy, tier)
            )
            for tier in MODULE.EXECUTION_TIERS
        }
        self.assertEqual(
            {"PR": 77, "Main": 189, "Nightly": 19, "Release": 17},
            actual,
        )

    def test_main_testrail_report_counts_are_locked(self) -> None:
        self.assertEqual(
            {
                "backend-unit": 12,
                "backend-integration": 26,
                "frontend-unit": 3,
                "checkout-e2e": 4,
            },
            MODULE.validate_main_report_counts(
                self.policy,
                self.automation_map,
                self.classified,
            ),
        )

    def test_main_partial_project_filters_exclude_nightly(self) -> None:
        expected = {
            "inventory-service": 14,
            "messaging-integration": 4,
            "orders-service": 9,
        }
        actual = {
            source: MODULE.build_filter(
                self.classified,
                self.policy,
                "main",
                source,
            ).count("FullyQualifiedName~")
            for source in expected
        }
        self.assertEqual(expected, actual)

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
                "api-gateway": 1,
                "basket-service": 1,
                "catalog-service": 2,
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

    def test_control_character_selector_fails_closed(self) -> None:
        changed = json.loads(json.dumps(self.automation_map))
        changed["intents"]["ESHOP-AUTH-001"].append("UnsafeTests.Name\ninjected=x")
        with self.assertRaisesRegex(MODULE.PolicyError, "invalid selectors"):
            MODULE.automation_selectors(changed)

    def test_ci_workflow_enforces_event_tier_contract(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        main_only_steps = (
            "Verify Docker availability",
            "Run API Gateway integration tests",
            "Run Basket Service integration tests",
            "Run messaging integration tests",
            "Run Catalog Service integration tests",
            "Run Orders Service integration tests",
            "Run Inventory Service integration tests",
            "Run Payments Service integration tests",
            "Run Notifications Service integration tests",
        )
        for step in main_only_steps:
            self.assertIn(
                f"- name: {step}\n        if: ${{{{ github.event_name != 'pull_request' }}}}",
                workflow,
            )

        self.assertIn(
            "containers:\n    name: Container images\n"
            "    if: ${{ github.event_name != 'pull_request' }}",
            workflow,
        )
        self.assertIn(
            "e2e:\n    name: Checkout E2E\n"
            "    if: ${{ github.event_name != 'pull_request' }}",
            workflow,
        )
        self.assertIn("github.event_name != 'pull_request'", workflow)

    def test_ci_workflow_consumes_generated_main_filters(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        for source, output in (
            ("inventory-service", "inventory_main_filter"),
            ("messaging-integration", "messaging_main_filter"),
            ("orders-service", "orders_main_filter"),
        ):
            self.assertIn(
                f"filter --tier main --source {source}",
                workflow,
            )
            self.assertIn(
                f"needs.quality-policy.outputs.{output}",
                workflow,
            )


if __name__ == "__main__":
    unittest.main()
