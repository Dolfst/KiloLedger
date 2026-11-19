#!/usr/bin/env python3
"""Offline checks for 1.18.0 id-aware duplicate detection."""
from __future__ import annotations

import re

ID_RE = re.compile(r"^[cfrukepxmnt]_[0-9a-fA-F]{8,}$")


def looks_like_id(value: str) -> bool:
    return bool(value and ID_RE.match(value))


def has_id(row: list[str]) -> bool:
    return bool(row) and looks_like_id(row[-1])


def get_id(row: list[str]) -> str | None:
    return row[-1] if has_id(row) else None


def strip_id(row: list[str]) -> list[str]:
    return row[:-1] if has_id(row) else list(row)


def natural_key(row: list[str], cols: list[int], ignore_case: list[bool]) -> str | None:
    content = strip_id(row)
    if len(content) <= cols[-1]:
        return None
    parts = []
    for j, c in enumerate(cols):
        value = content[c] or ""
        if ignore_case[j]:
            value = value.upper()
        parts.append(value)
    return "\x1f".join(parts)


def content_key(row: list[str]) -> str:
    return "\x1f".join(strip_id(row))


def find_groups(rows: list[list[str]], cols: list[int], ignore_case: list[bool]) -> list[tuple[str, list[int]]]:
    groups: list[tuple[str, list[int]]] = []
    by_key: dict[str, list[int]] = {}
    for i, row in enumerate(rows):
        key = natural_key(row, cols, ignore_case)
        if key is None:
            continue
        by_key.setdefault(key, []).append(i)
    for key, idxs in by_key.items():
        ids = {get_id(rows[i]) or f"#idx:{i}" for i in idxs}
        if len(idxs) >= 2 and len(ids) >= 2:
            groups.append(("ключ", idxs))

    by_content: dict[str, list[int]] = {}
    for i, row in enumerate(rows):
        by_content.setdefault(content_key(row), []).append(i)
    for key, idxs in by_content.items():
        ids = {get_id(rows[i]) or f"#idx:{i}" for i in idxs}
        if len(idxs) >= 2 and len(ids) >= 2:
            if any(g[1] == idxs for g in groups):
                continue
            groups.append(("содержание", idxs))
    return groups


def main() -> None:
    fuel_cols = [0, 1, 2]
    fuel_ic = [False, True, False]

    # Same id → not a duplicate group (only one unique id).
    same_id = [
        ["2024-01-01", "Car", "100", "40", "100", "f_aaaaaaaa"],
        ["2024-01-01", "Car", "100", "40", "100", "f_aaaaaaaa"],
    ]
    assert find_groups(same_id, fuel_cols, fuel_ic) == []

    # Same natural key, different ids → duplicate.
    key_dups = [
        ["2024-01-01", "Car", "100", "40", "100", "2.5", "", "1", "f_aaaaaaaa"],
        ["2024-01-01", "car", "100", "41", "110", "2.6", "x", "1", "f_bbbbbbbb"],
    ]
    g = find_groups(key_dups, fuel_cols, fuel_ic)
    assert len(g) == 1 and g[0][0] == "ключ" and g[0][1] == [0, 1]

    # Identical content, different ids (also same key) → one group via key path.
    content_dups = [
        ["2024-02-01", "A", "10", "1", "2", "f_11111111"],
        ["2024-02-01", "A", "10", "1", "2", "f_22222222"],
    ]
    g2 = find_groups(content_dups, fuel_cols, fuel_ic)
    assert len(g2) == 1

    # Different keys, identical content on non-key fields only — not content-equal.
    different = [
        ["2024-03-01", "A", "10", "1", "2", "f_11111111"],
        ["2024-03-02", "A", "10", "1", "2", "f_22222222"],
    ]
    assert find_groups(different, fuel_cols, fuel_ic) == []

    # Content-equal with different keys impossible for fuel (key ⊂ content).
    # Expenses: same content fields beyond key still caught by content if keys differ?
    # date|car|cat|name|sum — if only sum differs, neither key nor content match.
    print("verify_duplicates: ok")


if __name__ == "__main__":
    main()
