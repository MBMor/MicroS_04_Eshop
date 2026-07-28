"""JUnit parsing and TestIntent aggregation helpers (standard library only)."""

from __future__ import annotations

import copy
import json
import re
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


AGGREGATE_CLASSNAME = "Eshop.TestIntents"


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


@dataclass(frozen=True)
class RawCase:
    classname: str
    name: str
    time: float
    element: ET.Element
    source: Path

    @property
    def automation_id(self) -> str:
        return f"{self.classname}.{self.name}"

    @property
    def status(self) -> str:
        children = {_local_name(child.tag) for child in self.element}
        if "failure" in children or "error" in children:
            return "failed"
        if "skipped" in children:
            return "skipped"
        return "passed"


def read_cases(paths: Iterable[Path]) -> list[RawCase]:
    cases: list[RawCase] = []
    for path in paths:
        root = ET.parse(path).getroot()
        if _local_name(root.tag) not in {"testsuite", "testsuites"}:
            raise ValueError(f"{path}: root must be <testsuite> or <testsuites>")
        for element in root.iter():
            if _local_name(element.tag) != "testcase":
                continue
            classname = (element.get("classname") or "").strip()
            name = (element.get("name") or "").strip()
            if not classname or not name:
                raise ValueError(f"{path}: every testcase needs classname and name")
            try:
                duration = float(element.get("time", "0") or 0)
            except ValueError as exc:
                raise ValueError(f"{path}: invalid testcase time") from exc
            cases.append(RawCase(classname, name, duration, element, path))
    return cases


def canonical_selector(case: RawCase) -> str:
    """Return the stable source selector used by the governed binding manifest."""
    has_suite_hierarchy = bool(re.search(r"\s+(?:>|›)\s+", case.name))
    name = re.sub(r"\s+(?:>|›)\s+", ".", case.name).strip()
    path_class = case.classname.replace("\\", "/")

    if path_class.endswith(('.test.ts', '.test.tsx')):
        return name

    if path_class.endswith(('.spec.ts', '.spec.tsx')):
        stem = Path(path_class).name.rsplit(".ts", 1)[0]
        return name if has_suite_hierarchy or name.startswith(f"{stem}.") else f"{stem}.{name}"

    class_name = case.classname.rsplit(".", 1)[-1]
    method_name = name
    if method_name.startswith(f"{class_name}."):
        method_name = method_name[len(class_name) + 1 :]
    method_name = method_name.split("(", 1)[0].strip()
    return f"{class_name}.{method_name}"


def load_bindings(path: Path) -> dict[str, tuple[str, ...]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("version") != 1 or not isinstance(payload.get("intents"), dict):
        raise ValueError(f"{path}: unsupported binding manifest")
    reverse: dict[str, list[str]] = defaultdict(list)
    for intent, selectors in payload["intents"].items():
        if not re.fullmatch(r"ESHOP-[A-Z0-9]+-\d{3}", intent):
            raise ValueError(f"{path}: invalid TestIntent reference {intent!r}")
        if not isinstance(selectors, list) or not selectors:
            raise ValueError(f"{path}: {intent} must have at least one selector")
        for selector in selectors:
            selector = selector.split("; variants=", 1)[0]
            reverse[selector].append(intent)
    return {selector: tuple(sorted(set(intents))) for selector, intents in reverse.items()}


def merge_junit(paths: Iterable[Path], output: Path, name: str) -> None:
    suites: list[ET.Element] = []
    for path in paths:
        root = ET.parse(path).getroot()
        if _local_name(root.tag) == "testsuite":
            suites.append(copy.deepcopy(root))
        elif _local_name(root.tag) == "testsuites":
            suites.extend(copy.deepcopy(child) for child in root if _local_name(child.tag) == "testsuite")
        else:
            raise ValueError(f"{path}: unsupported JUnit root")

    root = ET.Element("testsuites", {"name": name})
    for suite in suites:
        root.append(suite)
    _set_counts(root)
    _write_xml(root, output)


def aggregate_junit(
    paths: Iterable[Path], output: Path, run_name: str, bindings_path: Path
) -> list[str]:
    cases = read_cases(paths)
    bindings = load_bindings(bindings_path)
    grouped: dict[str, list[RawCase]] = defaultdict(list)
    unknown: list[str] = []

    for case in cases:
        selector = canonical_selector(case)
        intents = bindings.get(selector)
        if not intents:
            unknown.append(f"{selector}  [raw: {case.automation_id}]")
            continue
        for intent in intents:
            grouped[intent].append(case)

    if unknown:
        details = "\n  - ".join(sorted(set(unknown)))
        raise ValueError(f"Unmapped raw test cases:\n  - {details}")
    if not grouped:
        raise ValueError("No mapped test cases were found")

    suite = ET.Element("testsuite", {"name": run_name})
    for intent in sorted(grouped):
        source_cases = grouped[intent]
        testcase = ET.SubElement(
            suite,
            "testcase",
            {
                "classname": AGGREGATE_CLASSNAME,
                "name": intent,
                "time": _format_time(sum(case.time for case in source_cases)),
            },
        )
        statuses = {case.status for case in source_cases}
        if "failed" in statuses:
            failure = ET.SubElement(testcase, "failure", {"message": "One or more bound tests failed"})
            failure.text = "\n".join(
                case.automation_id for case in source_cases if case.status == "failed"
            )
        elif "skipped" in statuses:
            ET.SubElement(testcase, "skipped", {"message": "One or more bound tests were skipped"})
        system_out = ET.SubElement(testcase, "system-out")
        system_out.text = "Bound raw Automation IDs:\n" + "\n".join(
            sorted(case.automation_id for case in source_cases)
        )

    _set_counts(suite)
    root = ET.Element("testsuites", {"name": run_name})
    root.append(suite)
    _set_counts(root)
    _write_xml(root, output)
    return [f"{AGGREGATE_CLASSNAME}.{intent}" for intent in sorted(grouped)]


def automation_ids(paths: Iterable[Path]) -> list[str]:
    return sorted({case.automation_id for case in read_cases(paths)})


def _set_counts(element: ET.Element) -> None:
    cases = [child for child in element.iter() if _local_name(child.tag) == "testcase"]
    child_kinds = [{_local_name(child.tag) for child in case} for case in cases]
    element.set("tests", str(len(cases)))
    element.set("failures", str(sum("failure" in kinds for kinds in child_kinds)))
    element.set("errors", str(sum("error" in kinds for kinds in child_kinds)))
    element.set("skipped", str(sum("skipped" in kinds for kinds in child_kinds)))
    element.set("time", _format_time(sum(float(case.get("time", "0") or 0) for case in cases)))


def _format_time(value: float) -> str:
    return f"{value:.6f}".rstrip("0").rstrip(".") or "0"


def _write_xml(root: ET.Element, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    ET.indent(root, space="  ")
    ET.ElementTree(root).write(output, encoding="utf-8", xml_declaration=True)
