from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_POLICY = Path(__file__).with_name("test-tier-policy.json")
DEFAULT_AUTOMATION_MAP = (
    REPOSITORY_ROOT / "scripts" / "testrail" / "automation-id-map.json"
)
PRIMARY_TIERS = ("PR", "Main", "Nightly")
EXECUTION_TIERS = ("PR", "Main", "Nightly", "Release")


class PolicyError(ValueError):
    pass


@dataclass(frozen=True)
class ClassifiedSelector:
    selector: str
    source_id: str
    kind: str
    project: str
    primary_tier: str
    release: bool


def load_json(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as stream:
        value = json.load(stream)

    if not isinstance(value, dict):
        raise PolicyError(f"Expected a JSON object in {path}.")

    return value


def automation_selectors(automation_map: dict[str, Any]) -> set[str]:
    intents = automation_map.get("intents")
    if not isinstance(intents, dict):
        raise PolicyError("Automation map must contain an 'intents' object.")

    selectors: set[str] = set()
    for intent, values in intents.items():
        if not isinstance(values, list) or not all(
            isinstance(value, str)
            and value.strip()
            and all(character >= " " and character != "\x7f" for character in value)
            for value in values
        ):
            raise PolicyError(f"Intent {intent!r} has invalid selectors.")
        selectors.update(values)

    return selectors


def classify(
    policy: dict[str, Any],
    selectors: set[str],
) -> list[ClassifiedSelector]:
    sources = policy.get("sources")
    if not isinstance(sources, list) or not sources:
        raise PolicyError("Tier policy must contain a non-empty 'sources' list.")

    nightly = selector_set(policy, "nightly_selectors")
    release = selector_set(policy, "release_selectors")
    unknown_overrides = (nightly | release) - selectors
    if unknown_overrides:
        raise PolicyError(
            "Tier policy references unknown selectors: "
            + ", ".join(sorted(unknown_overrides))
        )

    classified: list[ClassifiedSelector] = []
    for selector in sorted(selectors):
        matches: list[dict[str, Any]] = []
        for source in sources:
            prefixes = source.get("selector_prefixes", [])
            if any(selector.startswith(f"{prefix}.") for prefix in prefixes):
                matches.append(source)

        if len(matches) != 1:
            raise PolicyError(
                f"Selector {selector!r} matched {len(matches)} source groups; "
                "exactly one is required."
            )

        source = matches[0]
        default_tier = source.get("default_primary_tier")
        if default_tier not in PRIMARY_TIERS:
            raise PolicyError(
                f"Source {source.get('id')!r} has invalid primary tier "
                f"{default_tier!r}."
            )

        project = source.get("project")
        kind = source.get("kind")
        source_id = source.get("id")
        if not all(
            isinstance(value, str) and value.strip()
            for value in (project, kind, source_id)
        ):
            raise PolicyError("Every source needs non-empty id, kind and project.")

        primary_tier = "Nightly" if selector in nightly else default_tier
        classified.append(
            ClassifiedSelector(
                selector=selector,
                source_id=source_id,
                kind=kind,
                project=project,
                primary_tier=primary_tier,
                release=selector in release,
            )
        )

    validate_counts(policy, classified)
    return classified


def selector_set(policy: dict[str, Any], field: str) -> set[str]:
    values = policy.get(field)
    if not isinstance(values, list) or not all(
        isinstance(value, str) and value.strip() for value in values
    ):
        raise PolicyError(f"Tier policy field {field!r} must be a string list.")
    if len(values) != len(set(values)):
        raise PolicyError(f"Tier policy field {field!r} contains duplicates.")
    return set(values)


def validate_counts(
    policy: dict[str, Any],
    classified: list[ClassifiedSelector],
) -> None:
    actual = Counter(item.primary_tier for item in classified)
    actual["Release"] = sum(item.release for item in classified)
    expected = policy.get("expected_counts")
    if not isinstance(expected, dict):
        raise PolicyError("Tier policy must contain 'expected_counts'.")

    for tier in (*PRIMARY_TIERS, "Release"):
        if expected.get(tier) != actual[tier]:
            raise PolicyError(
                f"Tier {tier} expected {expected.get(tier)!r} selectors "
                f"but classified {actual[tier]}."
            )


def validate_aggregate_counts(
    policy: dict[str, Any],
    automation_map: dict[str, Any],
) -> dict[str, tuple[int, int]]:
    intents = automation_map.get("intents")
    if not isinstance(intents, dict):
        raise PolicyError("Automation map must contain an 'intents' object.")

    expected_aggregates = policy.get("expected_aggregate_counts")
    expected_edges = policy.get("expected_mapping_edges")
    if not isinstance(expected_aggregates, dict) or not isinstance(
        expected_edges, dict
    ):
        raise PolicyError(
            "Tier policy must contain expected aggregate and mapping-edge counts."
        )

    classified = classify(policy, automation_selectors(automation_map))
    expected_execution = policy.get("expected_execution_selector_counts")
    if not isinstance(expected_execution, dict):
        raise PolicyError(
            "Tier policy must contain expected execution selector counts."
        )

    actual: dict[str, tuple[int, int]] = {}
    for tier in EXECUTION_TIERS:
        selected = execution_selectors(classified, policy, tier)
        if expected_execution.get(tier) != len(selected):
            raise PolicyError(
                f"Tier {tier} expected {expected_execution.get(tier)!r} "
                f"execution selectors but selected {len(selected)}."
            )
        aggregate_count = 0
        edge_count = 0
        for values in intents.values():
            if not isinstance(values, list):
                raise PolicyError("Every TestIntent mapping must be a list.")
            selected_edges = selected.intersection(values)
            if selected_edges:
                aggregate_count += 1
                edge_count += len(selected_edges)

        actual[tier] = (aggregate_count, edge_count)
        if expected_aggregates.get(tier) != aggregate_count:
            raise PolicyError(
                f"Tier {tier} expected {expected_aggregates.get(tier)!r} "
                f"TestIntent aggregates but produced {aggregate_count}."
            )
        if expected_edges.get(tier) != edge_count:
            raise PolicyError(
                f"Tier {tier} expected {expected_edges.get(tier)!r} mapping edges "
                f"but produced {edge_count}."
            )

    validate_main_report_counts(policy, automation_map, classified)
    return actual


def execution_selectors(
    classified: list[ClassifiedSelector] | None,
    policy: dict[str, Any],
    tier: str,
) -> set[str]:
    normalized = tier.casefold()
    if normalized == "release":
        return selector_set(policy, "release_selectors")
    if classified is None:
        raise PolicyError(f"Classified selectors are required for tier {tier}.")
    if normalized == "pr":
        return {item.selector for item in classified if item.primary_tier == "PR"}
    if normalized == "main":
        return {
            item.selector
            for item in classified
            if item.primary_tier in {"PR", "Main"}
        }
    if normalized == "nightly":
        return {
            item.selector for item in classified if item.primary_tier == "Nightly"
        }
    raise PolicyError(f"Unsupported execution tier: {tier}.")


def validate_main_report_counts(
    policy: dict[str, Any],
    automation_map: dict[str, Any],
    classified: list[ClassifiedSelector],
) -> dict[str, int]:
    expected = policy.get("expected_main_report_counts")
    if not isinstance(expected, dict):
        raise PolicyError("Tier policy must contain expected Main report counts.")

    intents = automation_map["intents"]
    selected = execution_selectors(classified, policy, "Main")
    areas: dict[str, set[str]] = {
        "backend-unit": set(),
        "backend-integration": set(),
        "frontend-unit": set(),
        "checkout-e2e": set(),
    }
    for item in classified:
        if item.selector not in selected:
            continue
        if item.source_id == "backend-unit":
            area = "backend-unit"
        elif item.kind == "dotnet":
            area = "backend-integration"
        elif item.kind == "vitest":
            area = "frontend-unit"
        elif item.kind == "playwright":
            area = "checkout-e2e"
        else:
            raise PolicyError(f"Unsupported Main report kind: {item.kind}.")
        areas[area].add(item.selector)

    actual = {
        area: sum(bool(selectors.intersection(values)) for values in intents.values())
        for area, selectors in areas.items()
    }
    if expected != actual:
        raise PolicyError(
            f"Main TestRail report counts expected {expected!r} but produced {actual!r}."
        )
    return actual


def build_matrix(
    classified: list[ClassifiedSelector],
    tier: str,
    policy: dict[str, Any] | None = None,
) -> dict[str, list[dict[str, str | int]]]:
    normalized = tier.casefold()
    if normalized not in {"nightly", "release"}:
        raise PolicyError("Executable matrix is supported for Nightly or Release.")

    selected_names = execution_selectors(
        classified,
        policy if policy is not None else load_json(DEFAULT_POLICY),
        tier,
    )
    selected = [item for item in classified if item.selector in selected_names]
    unsupported = sorted({item.kind for item in selected if item.kind != "dotnet"})
    if unsupported:
        raise PolicyError(
            f"Tier {tier} contains unsupported runner kinds: {', '.join(unsupported)}."
        )

    grouped: dict[tuple[str, str], list[str]] = defaultdict(list)
    for item in selected:
        grouped[(item.source_id, item.project)].append(item.selector)

    include = []
    for (source_id, project), selectors in sorted(grouped.items()):
        include.append(
            {
                "name": source_id,
                "project": project,
                "filter": "|".join(
                    f"FullyQualifiedName~{selector}" for selector in sorted(selectors)
                ),
                "logical_tests": len(selectors),
            }
        )

    if not include:
        raise PolicyError(f"Tier {tier} selected no executable projects.")

    return {"include": include}


def build_filter(
    classified: list[ClassifiedSelector],
    policy: dict[str, Any],
    tier: str,
    source_id: str,
) -> str:
    source_items = [item for item in classified if item.source_id == source_id]
    if not source_items:
        raise PolicyError(f"Unknown source group: {source_id}.")
    if any(item.kind != "dotnet" for item in source_items):
        raise PolicyError(f"Source {source_id} is not a .NET test source.")

    selected_names = execution_selectors(classified, policy, tier)
    selectors = sorted(
        item.selector for item in source_items if item.selector in selected_names
    )
    if not selectors:
        raise PolicyError(f"Tier {tier} selects no tests from source {source_id}.")
    unsafe = [
        selector
        for selector in selectors
        if re.fullmatch(r"[A-Za-z0-9_.]+", selector) is None
    ]
    if unsafe:
        raise PolicyError(
            f"Source {source_id} contains selectors unsafe for a VSTest filter."
        )
    return "|".join(f"FullyQualifiedName~{selector}" for selector in selectors)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate and materialize the governed test tier policy."
    )
    parser.add_argument(
        "command",
        choices=("validate", "matrix", "filter"),
    )
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument(
        "--automation-map",
        type=Path,
        default=DEFAULT_AUTOMATION_MAP,
    )
    parser.add_argument(
        "--tier",
        choices=("pr", "main", "nightly", "release"),
    )
    parser.add_argument("--source")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        policy = load_json(args.policy)
        automation_map = load_json(args.automation_map)
        selectors = automation_selectors(automation_map)
        classified = classify(policy, selectors)
        aggregate_counts = validate_aggregate_counts(policy, automation_map)

        if args.command == "matrix":
            if args.tier is None:
                raise PolicyError("--tier is required for the matrix command.")
            print(
                json.dumps(
                    build_matrix(classified, args.tier, policy),
                    separators=(",", ":"),
                )
            )
        elif args.command == "filter":
            if args.tier is None or args.source is None:
                raise PolicyError("--tier and --source are required for filter.")
            print(build_filter(classified, policy, args.tier, args.source))
        else:
            counts = Counter(item.primary_tier for item in classified)
            execution_counts = {
                tier: len(execution_selectors(classified, policy, tier))
                for tier in EXECUTION_TIERS
            }
            print(
                "Tier policy valid: "
                f"selectors={len(classified)}, "
                f"PR={counts['PR']}, Main={counts['Main']}, "
                f"Nightly={counts['Nightly']}, "
                f"Release={sum(item.release for item in classified)}; "
                f"execution selectors PR={execution_counts['PR']}, "
                f"Main={execution_counts['Main']}, "
                f"Nightly={execution_counts['Nightly']}, "
                f"Release={execution_counts['Release']}; "
                f"TestRail PR={aggregate_counts['PR'][0]}/"
                f"{aggregate_counts['PR'][1]} edges, "
                f"Main={aggregate_counts['Main'][0]}/"
                f"{aggregate_counts['Main'][1]} edges, "
                f"Nightly={aggregate_counts['Nightly'][0]}/"
                f"{aggregate_counts['Nightly'][1]} edges, "
                f"Release={aggregate_counts['Release'][0]}/"
                f"{aggregate_counts['Release'][1]} edges"
            )
        return 0
    except (OSError, json.JSONDecodeError, PolicyError) as error:
        print(f"Tier policy invalid: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
