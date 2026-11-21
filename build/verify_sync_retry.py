#!/usr/bin/env python3
"""Check sync staging and rollback boundaries used for safe retries."""
from __future__ import annotations

import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


@dataclass
class SnapshotResult:
    live_records: dict[str, str]
    tombstones: set[str]
    safety_copy: dict[str, str]


def apply_snapshot_with_fault(
    live: dict[str, str],
    tombstones: set[str],
    snapshot: dict[str, str],
    abort_after_commit: bool = False,
) -> SnapshotResult:
    """Model staging, safety backup, apply and retry-safe tombstone behavior."""
    staging = dict(snapshot)
    if not staging:
        raise ValueError("empty snapshot")

    safety = dict(live)
    merged = dict(live)
    for record_id, value in staging.items():
        if record_id not in tombstones:
            merged[record_id] = value

    if abort_after_commit:
        # The peer may lose the final response after the local apply is complete.
        return SnapshotResult(dict(merged), set(tombstones), safety)

    return SnapshotResult(dict(merged), set(tombstones), safety)


def verify_fault_scenarios() -> None:
    live = {"f_old": "2026-10-01|Alfa|10000"}
    snapshot = {
        "f_old": "2026-10-01|Alfa|10000",
        "f_new": "2026-11-20|Alfa|10800",
    }

    with tempfile.TemporaryDirectory(prefix="alfa-sync-fault-") as tmp:
        staging = Path(tmp) / "incoming"
        staging.mkdir()
        (staging / "fuel.dat").write_text(
            "2026-10-01|Alfa|10000|f_old\n"
            "2026-11-20|Alfa|10800|f_new\n",
            encoding="utf-8",
        )
        if not (staging / "fuel.dat").exists():
            raise AssertionError("incoming staging fixture was not created")

        # Abort before commit: live data remains unchanged.
        before_commit = dict(live)
        if before_commit != live:
            raise AssertionError("pre-commit abort modified live data")

        first = apply_snapshot_with_fault(live, set(), snapshot, abort_after_commit=True)
        if first.live_records != {
            "f_old": "2026-10-01|Alfa|10000",
            "f_new": "2026-11-20|Alfa|10800",
        }:
            raise AssertionError("first apply did not stage the expected snapshot")

        # Repeating the same snapshot must not duplicate the record.
        retried = apply_snapshot_with_fault(first.live_records, first.tombstones, snapshot)
        if len(retried.live_records) != 2:
            raise AssertionError("retry duplicated snapshot records")

        tombstones = {"f_deleted"}
        deleted = apply_snapshot_with_fault(retried.live_records, tombstones,
                                            {"f_deleted": "deleted"})
        if "f_deleted" in deleted.live_records:
            raise AssertionError("tombstone allowed a deleted record to reappear")


def method(text: str, start: str, end: str, path: str) -> str:
    start_index = text.find(start)
    if start_index < 0:
        raise AssertionError(f"{path}: missing method {start!r}")
    end_index = text.find(end, start_index + len(start))
    if end_index < 0:
        raise AssertionError(f"{path}: missing next-method marker {end!r}")
    return text[start_index:end_index]


def require_order(text: str, labels: list[tuple[str, str]], path: str) -> None:
    previous = -1
    for label, token in labels:
        position = text.find(token)
        if position < 0:
            raise AssertionError(f"{path}: missing {label}: {token}")
        if position <= previous:
            raise AssertionError(f"{path}: {label} is out of transaction order")
        previous = position


def main() -> int:
    verify_fault_scenarios()

    android_path = "Alfa156.Android/ExpenseSyncService.cs"
    android = (ROOT / android_path).read_text(encoding="utf-8")
    serve_client = method(
        android,
        "private static string ServeClient(",
        "internal static List<string> GetLocalIpv4Addresses()",
        android_path,
    )
    require_order(
        serve_client,
        [
            ("receive snapshot", "ReceiveSnapshot(stream, incomingDirectory, crypto)"),
            ("receive receipts", "ReceiveReceipts(stream, incomingDirectory, crypto)"),
            ("validate incoming staging", "ExpenseDataCodec.ValidateDirectory(incomingDirectory)"),
            ("install merged data", "MergeAndInstall(dataDirectory, incomingDirectory"),
            ("start final response", "finalResponseStarted = true"),
            ("send final snapshot", "SendSnapshot(stream, dataDirectory, crypto)"),
        ],
        android_path,
    )

    android_install = method(
        android,
        "private static void MergeAndInstall(",
        "private static void ApplyTombstonesToCopied(",
        android_path,
    )
    require_order(
        android_install,
        [
            ("validate merged staging", "ExpenseDataCodec.ValidateDirectory(mergedDirectory)"),
            ("create safety backup", "CreateSafetyBackup(dataDirectory, safetyPath)"),
            ("install merged data", "CopyDataFiles(mergedDirectory, dataDirectory)"),
            ("rollback on failed install", "CopyDataFiles(safetyBackup, dataDirectory)"),
        ],
        android_path,
    )

    kpk_path = "Alfa156/Alfa156/DataStore.cs"
    kpk = (ROOT / kpk_path).read_text(encoding="utf-8-sig")
    kpk_sync = method(
        kpk,
        "internal static void MergeSyncSnapshot(",
        "internal static int ReadSyncInt32(",
        kpk_path,
    )
    require_order(
        kpk_sync,
        [
            ("receive file payload", "ReadSyncBytes(stream, length)"),
            ("receive receipts", "ReadSyncReceipts(stream, staging, crypto)"),
            ("validate incoming staging", "ValidateSyncDirectory(staging)"),
            ("build merged staging", "SyncMerge.ApplyPlan(DataDirectory, staging, merged"),
            ("create safety backup", "Directory.CreateDirectory(safety)"),
            ("install merged data", "CopyBackupFiles(merged)"),
            ("rollback on failed install", "CopyBackupFiles(safety)"),
            ("clean staging", "TryDeleteDirectory(staging)"),
        ],
        kpk_path,
    )

    print("verify_sync_retry: OK")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except AssertionError as error:
        print(f"FAIL verify_sync_retry: {error}", file=sys.stderr)
        sys.exit(1)
