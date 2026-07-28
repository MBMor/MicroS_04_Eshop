#!/usr/bin/env python3
"""List the exact TestRail Automation IDs produced by JUnit reports."""

from __future__ import annotations

import argparse
from pathlib import Path

from junit_tools import automation_ids


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("reports", nargs="+", type=Path)
    args = parser.parse_args()
    for automation_id in automation_ids(args.reports):
        print(automation_id)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
