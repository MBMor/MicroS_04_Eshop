#!/usr/bin/env python3
"""Fail before TRCLI upload when local Automation IDs are absent or ambiguous."""

from __future__ import annotations

import argparse
import base64
import json
import urllib.error
import urllib.request
from collections import Counter
from pathlib import Path
from typing import Any

from junit_tools import automation_ids


class TestRailClient:
    def __init__(self, host: str, username: str, api_key: str) -> None:
        self.base = host.rstrip("/") + "/index.php?/api/v2/"
        self.authorization = base64.b64encode(f"{username}:{api_key}".encode()).decode()

    def get(self, endpoint: str) -> Any:
        request = urllib.request.Request(
            self.base + endpoint,
            headers={"Authorization": f"Basic {self.authorization}", "Accept": "application/json"},
        )
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return json.load(response)
        except urllib.error.HTTPError as exc:
            raise RuntimeError(f"TestRail API returned HTTP {exc.code} for {endpoint.split('&', 1)[0]}") from exc
        except urllib.error.URLError as exc:
            raise RuntimeError(f"TestRail API is unavailable: {exc.reason}") from exc

    def collection(self, endpoint: str, key: str) -> list[dict[str, Any]]:
        result: list[dict[str, Any]] = []
        offset = 0
        while True:
            payload = self.get(f"{endpoint}&limit=250&offset={offset}")
            if isinstance(payload, list):
                return payload
            page = payload.get(key, [])
            result.extend(page)
            size = int(payload.get("size", len(page)))
            if size < 250:
                return result
            offset += size


def compare_ids(local_ids: list[str], remote_ids: list[str]) -> tuple[list[str], list[str]]:
    counts = Counter(value for value in remote_ids if value)
    missing = sorted(value for value in local_ids if counts[value] == 0)
    duplicate = sorted(value for value in local_ids if counts[value] > 1)
    return missing, duplicate


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", required=True)
    parser.add_argument("--project", required=True)
    parser.add_argument("--username", required=True)
    parser.add_argument("--api-key", required=True)
    parser.add_argument("reports", nargs="+", type=Path)
    args = parser.parse_args()

    local = automation_ids(args.reports)
    client = TestRailClient(args.host, args.username, args.api_key)
    projects = client.collection("get_projects&is_completed=0", "projects")
    matches = [project for project in projects if project.get("name") == args.project]
    if len(matches) != 1:
        raise SystemExit(f"Expected exactly one active TestRail project named {args.project!r}; found {len(matches)}")

    project_id = matches[0]["id"]
    cases = client.collection(f"get_cases/{project_id}", "cases")
    remote = [
        str(case.get("custom_automation_id") or case.get("custom_case_automation_id") or "").strip()
        for case in cases
    ]
    missing, duplicate = compare_ids(local, remote)
    if missing or duplicate:
        if missing:
            print("::error::Missing TestRail Automation IDs:\n  - " + "\n  - ".join(missing))
        if duplicate:
            print("::error::Duplicate TestRail Automation IDs:\n  - " + "\n  - ".join(duplicate))
        return 1
    print(f"Validated {len(local)} unique Automation IDs against TestRail project {args.project!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
