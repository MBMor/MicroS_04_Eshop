#!/usr/bin/env python3
"""Decide and validate fail-closed TestRail publication for the Main CI workflow."""

from __future__ import annotations

import argparse
import json
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_POLICY = Path(__file__).with_name("test-tier-policy.json")
DEFAULT_REPORT_DIRECTORY = REPOSITORY_ROOT / "artifacts" / "testrail"
REPORT_FILES = {
    "backend-unit": "backend-unit.junit.xml",
    "backend-integration": "backend-integration.junit.xml",
    "frontend-unit": "frontend-unit.junit.xml",
    "checkout-e2e": "checkout-e2e.junit.xml",
}
PUBLISHING_EVENTS = {"push", "workflow_dispatch"}
REQUIRED_RESULTS = ("changes", "backend", "frontend", "e2e")


class PublicationPolicyError(ValueError):
    pass


@dataclass(frozen=True)
class PublicationDecision:
    publish: bool
    reason: str


def decide_publication(
    event_name: str,
    application_changed: str,
    job_results: dict[str, str],
) -> PublicationDecision:
    if event_name not in PUBLISHING_EVENTS:
        return PublicationDecision(
            False,
            f"event-{event_name or 'missing'}-not-publishable",
        )

    if application_changed != "true":
        return PublicationDecision(False, "documentation-only-or-unknown-scope")

    missing = [name for name in REQUIRED_RESULTS if name not in job_results]
    if missing:
        raise PublicationPolicyError(
            f"Missing required job results: {', '.join(missing)}."
        )

    unsuccessful = [
        f"{name}={job_results[name] or 'missing'}"
        for name in REQUIRED_RESULTS
        if job_results[name] != "success"
    ]
    if unsuccessful:
        return PublicationDecision(
            False,
            "required-jobs-not-successful-" + "-".join(unsuccessful),
        )

    return PublicationDecision(True, "all-required-jobs-successful")


def load_json(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as stream:
        value = json.load(stream)

    if not isinstance(value, dict):
        raise PublicationPolicyError(f"Expected a JSON object in {path}.")

    return value


def validate_reports(
    report_directory: Path,
    policy: dict[str, Any],
) -> dict[str, int]:
    expected = policy.get("expected_main_report_counts")
    if not isinstance(expected, dict) or set(expected) != set(REPORT_FILES):
        raise PublicationPolicyError(
            "Tier policy must define the exact expected Main report areas."
        )

    actual: dict[str, int] = {}
    for area, filename in REPORT_FILES.items():
        path = report_directory / filename
        if not path.is_file() or path.stat().st_size == 0:
            raise PublicationPolicyError(
                f"Required TestRail report is missing or empty: {path}."
            )

        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as error:
            raise PublicationPolicyError(
                f"Required TestRail report is invalid XML: {path}: {error}."
            ) from error

        count = sum(1 for _ in root.iter("testcase"))
        expected_count = expected[area]
        if not isinstance(expected_count, int) or expected_count <= 0:
            raise PublicationPolicyError(
                f"Expected report count for {area!r} must be a positive integer."
            )
        if count != expected_count:
            raise PublicationPolicyError(
                f"Report {path} contains {count} TestIntents; expected {expected_count}."
            )
        actual[area] = count

    return actual


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    decide = subparsers.add_parser("decide")
    decide.add_argument("--event-name", required=True)
    decide.add_argument("--application-changed", required=True)
    for name in REQUIRED_RESULTS:
        decide.add_argument(f"--{name}-result", required=True)

    validate = subparsers.add_parser("validate-reports")
    validate.add_argument(
        "--report-directory",
        type=Path,
        default=DEFAULT_REPORT_DIRECTORY,
    )
    validate.add_argument("--policy", type=Path, default=DEFAULT_POLICY)

    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        if arguments.command == "decide":
            decision = decide_publication(
                arguments.event_name,
                arguments.application_changed,
                {
                    name: getattr(arguments, f"{name}_result")
                    for name in REQUIRED_RESULTS
                },
            )
            print(f"publish={'true' if decision.publish else 'false'}")
            print(f"reason={decision.reason}")
            return 0

        counts = validate_reports(
            arguments.report_directory,
            load_json(arguments.policy),
        )
    except (OSError, json.JSONDecodeError, PublicationPolicyError) as error:
        print(f"TestRail publication policy invalid: {error}")
        return 1

    print(
        "TestRail report set valid: "
        + ", ".join(f"{area}={count}" for area, count in counts.items())
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
