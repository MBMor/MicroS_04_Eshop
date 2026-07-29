#!/usr/bin/env python3
"""Classify a Git change set for CI execution.

Documentation-only changes may skip application builds and tests. Empty or
unrecognised input fails closed and requests the full pipeline.
"""

from __future__ import annotations

import sys
from collections.abc import Iterable


def normalize(paths: Iterable[str]) -> tuple[str, ...]:
    return tuple(
        path.strip().replace("\\", "/") for path in paths if path.strip()
    )


def is_documentation(path: str) -> bool:
    return path.startswith("docs/") or path.lower().endswith(".md")


def application_changed(paths: Iterable[str]) -> bool:
    changed = normalize(paths)
    return not changed or any(not is_documentation(path) for path in changed)


def main() -> int:
    value = str(application_changed(sys.stdin)).lower()
    print(f"application_changed={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
