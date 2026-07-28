#!/usr/bin/env python3
"""Discover, merge and aggregate downloaded CI JUnit artifacts."""

from __future__ import annotations

import argparse
from pathlib import Path

from junit_tools import aggregate_junit, merge_junit


def _reports(root: Path) -> list[Path]:
    return sorted(root.rglob("*.junit.xml")) if root.exists() else []


def _prepare(name: str, reports: list[Path], output: Path, manifest: Path) -> None:
    if not reports:
        print(f"::warning::{name}: no JUnit report artifact found; TestRail run will be skipped")
        return
    target = output / f"{name}.junit.xml"
    ids = aggregate_junit(reports, target, name, manifest)
    print(f"Prepared {target} with {len(ids)} TestIntent results")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--backend-root", type=Path, required=True)
    parser.add_argument("--frontend-root", type=Path, required=True)
    parser.add_argument("--e2e-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path(__file__).with_name("automation-id-map.json"),
    )
    args = parser.parse_args()

    backend = _reports(args.backend_root)
    unit = [path for path in backend if "unit" in {part.lower() for part in path.parts}]
    integration = [path for path in backend if path not in unit]

    if integration:
        merge_junit(
            integration,
            args.output_dir / "backend-integration-raw-merged.junit.xml",
            "Backend Integration raw results",
        )
    _prepare("backend-unit", unit, args.output_dir, args.manifest)
    _prepare("backend-integration", integration, args.output_dir, args.manifest)
    _prepare("frontend-unit", _reports(args.frontend_root), args.output_dir, args.manifest)
    _prepare("checkout-e2e", _reports(args.e2e_root), args.output_dir, args.manifest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
