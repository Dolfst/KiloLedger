#!/usr/bin/env python3
"""Offline checks for 1.10.0 id + tombstone sync rules (mirrors C# logic)."""
from __future__ import annotations

import re
import uuid

ID_RE = re.compile(r"^[cfrukx]_[0-9a-fA-F]{8,}$")


def looks_like_id(value: str) -> bool:
    return bool(value and ID_RE.match(value))


def has_id(row: list[str]) -> bool:
    return bool(row) and looks_like_id(row[-1])


def get_id(row: list[str]) -> str | None:
    return row[-1] if has_id(row) else None


def strip_id(row: list[str]) -> list[str]:
    return row[:-1] if has_id(row) else list(row)


def with_id(row: list[str], rid: str) -> list[str]:
    return strip_id(row) + [rid]


def new_id(prefix: str) -> str:
    return prefix + uuid.uuid4().hex[:8]


def ensure_id(row: list[str], prefix: str) -> list[str]:
    return row if has_id(row) else with_id(row, new_id(prefix))


def content_equal(a: list[str], b: list[str]) -> bool:
    ca, cb = strip_id(a), strip_id(b)
    n = max(len(ca), len(cb))
    for i in range(n):
        left = ca[i] if i < len(ca) else ""
        right = cb[i] if i < len(cb) else ""
        if left != right:
            return False
    return True


def find_identity(rows: list[list[str]], candidate: list[str], keys: list[int]) -> int:
    cid = get_id(candidate)
    if cid:
        for i, row in enumerate(rows):
            if get_id(row) and get_id(row).lower() == cid.lower():
                return i
    cc = strip_id(candidate)
    for i, row in enumerate(rows):
        rc = strip_id(row)
        if any(k >= len(rc) or k >= len(cc) for k in keys):
            continue
        if all(rc[k].lower() == cc[k].lower() if k in (1,) else rc[k] == cc[k] for k in keys):
            # simplified: ignore-case only for car name col 1 when present in keys
            ok = True
            for k in keys:
                left, right = rc[k], cc[k]
                if k == 1:
                    if left.lower() != right.lower():
                        ok = False
                        break
                elif left != right:
                    ok = False
                    break
            if ok:
                return i
    return -1


def merge(local, remote, deleted: set[str], keys: list[int]):
    local = [ensure_id(r, "f_") for r in local]
    remote = [ensure_id(r, "f_") for r in remote]
    merged = []
    matched = [False] * len(local)
    removed = 0
    for rem in remote:
        rid = get_id(rem)
        if rid and rid in deleted:
            removed += 1
            continue
        m = find_identity(local, rem, keys)
        if m < 0:
            merged.append(rem)
            continue
        matched[m] = True
        lid = get_id(local[m])
        if lid and lid in deleted:
            removed += 1
            continue
        if content_equal(local[m], rem):
            merged.append(local[m])
        else:
            merged.append(local[m])  # prefer local
    for i, row in enumerate(local):
        if matched[i]:
            continue
        lid = get_id(row)
        if lid and lid in deleted:
            removed += 1
            continue
        merged.append(row)
    return merged, removed


def test_old_fuel_gets_id():
    row = ["2024-01-01", "Alfa", "100000", "40", "100"]
    assert not has_id(row)
    out = ensure_id(row, "f_")
    assert has_id(out)
    assert strip_id(out) == row
    assert len(out) == 6


def test_delete_propagates():
    a = [ensure_id(["2024-01-01", "Alfa", "100000", "40", "100"], "f_")]
    rid = get_id(a[0])
    b = [list(a[0])]  # same id on both sides
    # Android deletes
    deleted = {rid}
    a_after = []  # local deleted
    merged, removed = merge(a_after, b, deleted, [0, 1, 2])
    assert merged == []
    assert removed == 1


def test_natural_key_fallback():
    local = [ensure_id(["2024-01-01", "Alfa", "100000", "40", "100"], "f_")]
    # remote old row without id, same natural key, different liters
    remote = [["2024-01-01", "Alfa", "100000", "41", "100"]]
    merged, _ = merge(local, remote, set(), [0, 1, 2])
    assert len(merged) == 1
    assert get_id(merged[0]) == get_id(local[0])
    assert strip_id(merged[0])[3] == "40"  # prefer local content on conflict


def test_fuel_optional_fields_not_confused_with_id():
    row = ["2024-01-01", "Alfa", "100000", "40", "100", "2.5", "Shell"]
    out = ensure_id(row, "f_")
    assert strip_id(out) == row
    assert looks_like_id(out[-1])
    assert not looks_like_id("2.5")
    assert not looks_like_id("Shell")


if __name__ == "__main__":
    test_old_fuel_gets_id()
    test_delete_propagates()
    test_natural_key_fallback()
    test_fuel_optional_fields_not_confused_with_id()
    print("verify_sync_ids: OK")
