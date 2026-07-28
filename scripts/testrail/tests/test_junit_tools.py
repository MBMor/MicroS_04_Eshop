from __future__ import annotations

import json
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_DIR))

from junit_tools import aggregate_junit, automation_ids, canonical_selector, merge_junit, read_cases
from validate_testrail_automation_ids import compare_ids


class JUnitToolsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write(self, name: str, content: str) -> Path:
        path = self.root / name
        path.write_text(content, encoding="utf-8")
        return path

    def test_lists_exact_classname_dot_name(self) -> None:
        report = self.write("one.xml", '<testsuite><testcase classname="A.B" name="works"/></testsuite>')
        self.assertEqual(["A.B.works"], automation_ids([report]))

    def test_canonicalizes_dotnet_theory_and_vitest(self) -> None:
        dotnet = self.write(
            "dotnet.xml",
            '<testsuite><testcase classname="Example.ProductTests" name="Rejects(value: -1)"/></testsuite>',
        )
        vitest = self.write(
            "vitest.xml",
            '<testsuite><testcase classname="src/a.test.ts" name="apiRequest &gt; rejects input"/></testsuite>',
        )
        self.assertEqual("ProductTests.Rejects", canonical_selector(read_cases([dotnet])[0]))
        self.assertEqual("apiRequest.rejects input", canonical_selector(read_cases([vitest])[0]))

    def test_canonicalizes_playwright_describe_and_top_level_tests(self) -> None:
        report = self.write(
            "playwright.xml",
            '<testsuite><testcase classname="checkout-failure-paths.spec.ts" '
            'name="checkout failure paths › fails payment"/>'
            '<testcase classname="checkout-success.spec.ts" name="customer checks out"/></testsuite>',
        )
        cases = read_cases([report])
        self.assertEqual("checkout failure paths.fails payment", canonical_selector(cases[0]))
        self.assertEqual("checkout-success.spec.customer checks out", canonical_selector(cases[1]))

    def test_aggregate_is_many_to_many_and_failure_is_conservative(self) -> None:
        report = self.write(
            "raw.xml",
            '<testsuites><testsuite><testcase classname="N.OrderTests" name="Creates" time="1.2"/>'
            '<testcase classname="N.OrderTests" name="Fails" time="0.3"><failure/></testcase>'
            '</testsuite></testsuites>',
        )
        manifest = self.root / "map.json"
        manifest.write_text(
            json.dumps({"version": 1, "intents": {
                "ESHOP-E2E-001": ["OrderTests.Creates", "OrderTests.Fails"],
                "ESHOP-OUTBOX-001": ["OrderTests.Creates"],
            }}),
            encoding="utf-8",
        )
        output = self.root / "aggregate.xml"
        ids = aggregate_junit([report], output, "run", manifest)
        self.assertEqual(
            ["Eshop.TestIntents.ESHOP-E2E-001", "Eshop.TestIntents.ESHOP-OUTBOX-001"], ids
        )
        cases = read_cases([output])
        self.assertEqual("failed", next(case.status for case in cases if case.name == "ESHOP-E2E-001"))
        self.assertEqual("passed", next(case.status for case in cases if case.name == "ESHOP-OUTBOX-001"))

    def test_unknown_source_fails_closed(self) -> None:
        report = self.write("raw.xml", '<testsuite><testcase classname="N.C" name="NewTest"/></testsuite>')
        manifest = self.write(
            "map.json", '{"version":1,"intents":{"ESHOP-E2E-001":["C.Known"]}}'
        )
        with self.assertRaisesRegex(ValueError, "Unmapped raw test cases"):
            aggregate_junit([report], self.root / "out.xml", "run", manifest)

    def test_merge_recalculates_failure_error_and_skipped(self) -> None:
        first = self.write(
            "a.xml", '<testsuite><testcase classname="A" name="a"><error/></testcase></testsuite>'
        )
        second = self.write(
            "b.xml", '<testsuites><testsuite><testcase classname="B" name="b"><skipped/></testcase>'
            '<testcase classname="B" name="c"><failure/></testcase></testsuite></testsuites>'
        )
        output = self.root / "merged.xml"
        merge_junit([first, second], output, "merged")
        root = ET.parse(output).getroot()
        self.assertEqual(("3", "1", "1", "1"), tuple(root.get(key) for key in ("tests", "failures", "errors", "skipped")))

    def test_remote_comparison_reports_missing_and_duplicates(self) -> None:
        self.assertEqual(
            (["B"], ["A"]),
            compare_ids(["A", "B"], ["A", "A", "C"]),
        )


if __name__ == "__main__":
    unittest.main()
