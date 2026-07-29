from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "change_scope.py"
SPEC = importlib.util.spec_from_file_location("change_scope", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class ChangeScopeTests(unittest.TestCase):
    def test_nested_docs_are_documentation_only(self) -> None:
        self.assertFalse(
            MODULE.application_changed(
                ["docs/testing/evidence-baseline.md", "docs/architecture/context.puml"]
            )
        )

    def test_markdown_outside_docs_is_documentation_only(self) -> None:
        self.assertFalse(
            MODULE.application_changed(["README.md", ".github/CONTRIBUTING.md"])
        )

    def test_application_or_workflow_change_runs_full_pipeline(self) -> None:
        self.assertTrue(MODULE.application_changed(["src/backend/Program.cs"]))
        self.assertTrue(MODULE.application_changed([".github/workflows/ci.yml"]))

    def test_mixed_change_runs_full_pipeline(self) -> None:
        self.assertTrue(
            MODULE.application_changed(
                ["docs/testing/README.md", "frontend/package.json"]
            )
        )

    def test_empty_change_set_fails_closed(self) -> None:
        self.assertTrue(MODULE.application_changed([]))


if __name__ == "__main__":
    unittest.main()
