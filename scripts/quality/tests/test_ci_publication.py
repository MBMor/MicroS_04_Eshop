from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "ci_publication.py"
CI_WORKFLOW = Path(__file__).parents[3] / ".github" / "workflows" / "ci.yml"
SPEC = importlib.util.spec_from_file_location("ci_publication", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class PublicationDecisionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.successful = {
            "changes": "success",
            "backend": "success",
            "frontend": "success",
            "e2e": "success",
        }

    def test_successful_main_application_change_is_publishable(self) -> None:
        decision = MODULE.decide_publication("push", "true", self.successful)
        self.assertTrue(decision.publish)
        self.assertEqual("all-required-jobs-successful", decision.reason)

    def test_manual_full_run_is_publishable(self) -> None:
        decision = MODULE.decide_publication(
            "workflow_dispatch",
            "true",
            self.successful,
        )
        self.assertTrue(decision.publish)

    def test_failed_skipped_or_cancelled_job_fails_closed(self) -> None:
        for job in MODULE.REQUIRED_RESULTS:
            for result in ("failure", "skipped", "cancelled", ""):
                with self.subTest(job=job, result=result):
                    changed = dict(self.successful)
                    changed[job] = result
                    self.assertFalse(
                        MODULE.decide_publication(
                            "push",
                            "true",
                            changed,
                        ).publish
                    )

    def test_pull_request_and_docs_only_change_do_not_publish(self) -> None:
        self.assertFalse(
            MODULE.decide_publication(
                "pull_request",
                "true",
                self.successful,
            ).publish
        )
        self.assertFalse(
            MODULE.decide_publication("push", "false", self.successful).publish
        )

    def test_missing_job_result_is_invalid_policy_input(self) -> None:
        changed = dict(self.successful)
        del changed["frontend"]
        with self.assertRaisesRegex(MODULE.PublicationPolicyError, "Missing"):
            MODULE.decide_publication("push", "true", changed)


class PublicationReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.policy = MODULE.load_json(MODULE.DEFAULT_POLICY)
        self.temporary = tempfile.TemporaryDirectory()
        self.report_directory = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_report(self, area: str, count: int) -> None:
        root = ET.Element("testsuite", name=area, tests=str(count))
        for index in range(count):
            ET.SubElement(
                root,
                "testcase",
                classname="Eshop.TestIntents",
                name=f"ESHOP-{area.upper()}-{index:03d}",
            )
        ET.ElementTree(root).write(
            self.report_directory / MODULE.REPORT_FILES[area],
            encoding="utf-8",
            xml_declaration=True,
        )

    def write_complete_report_set(self) -> None:
        for area, count in self.policy["expected_main_report_counts"].items():
            self.write_report(area, count)

    def test_complete_report_set_matches_locked_cardinality(self) -> None:
        self.write_complete_report_set()
        self.assertEqual(
            self.policy["expected_main_report_counts"],
            MODULE.validate_reports(self.report_directory, self.policy),
        )

    def test_missing_report_fails_before_publication(self) -> None:
        self.write_complete_report_set()
        (self.report_directory / "checkout-e2e.junit.xml").unlink()
        with self.assertRaisesRegex(MODULE.PublicationPolicyError, "missing or empty"):
            MODULE.validate_reports(self.report_directory, self.policy)

    def test_wrong_aggregate_count_fails_before_publication(self) -> None:
        self.write_complete_report_set()
        self.write_report("checkout-e2e", 3)
        with self.assertRaisesRegex(MODULE.PublicationPolicyError, "expected 4"):
            MODULE.validate_reports(self.report_directory, self.policy)

    def test_malformed_report_fails_before_publication(self) -> None:
        self.write_complete_report_set()
        (self.report_directory / "frontend-unit.junit.xml").write_text(
            "<testsuite>",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(MODULE.PublicationPolicyError, "invalid XML"):
            MODULE.validate_reports(self.report_directory, self.policy)


class PublicationWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = CI_WORKFLOW.read_text(encoding="utf-8")

    def test_publication_gate_observes_every_required_job(self) -> None:
        self.assertIn(
            "publication_gate:\n"
            "    name: TestRail publication gate\n"
            "    needs:\n"
            "      - changes\n"
            "      - backend\n"
            "      - frontend\n"
            "      - e2e",
            self.workflow,
        )
        for result in ("changes", "backend", "frontend", "e2e"):
            self.assertIn(
                f'--{result}-result "${{{{ needs.{result}.result }}}}"',
                self.workflow,
            )

    def test_testrail_job_requires_positive_gate_output(self) -> None:
        self.assertIn(
            "if: ${{ always() && "
            "needs.publication_gate.outputs.publish == 'true' && "
            "!cancelled() }}",
            self.workflow,
        )

    def test_complete_report_validation_precedes_remote_operations(self) -> None:
        report_validation = self.workflow.index(
            "- name: Validate complete TestRail report set"
        )
        identity_validation = self.workflow.index(
            "- name: Validate Automation IDs exist uniquely in TestRail"
        )
        publication = self.workflow.index("- name: Publish separate TestRail runs")
        self.assertLess(report_validation, identity_validation)
        self.assertLess(identity_validation, publication)

    def test_artifact_download_does_not_tolerate_missing_reports(self) -> None:
        download_section = self.workflow[
            self.workflow.index("- name: Download backend test results") :
            self.workflow.index("- name: Aggregate raw results by TestIntent")
        ]
        self.assertNotIn("continue-on-error", download_section)

    def test_quality_job_exercises_e2e_shell_contract(self) -> None:
        self.assertIn("- name: Validate E2E shell portability", self.workflow)
        self.assertIn("bash -n \\", self.workflow)
        self.assertIn("shellcheck --severity=error \\", self.workflow)
        self.assertEqual(
            2,
            self.workflow.count("scripts/dev/*.sh"),
        )
        self.assertIn(
            "bash scripts/e2e/tests/port-check.test.sh",
            self.workflow,
        )


if __name__ == "__main__":
    unittest.main()
