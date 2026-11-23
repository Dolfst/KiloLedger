#!/usr/bin/env python3
"""Sanity-check currency key helpers and Wi-Fi receipt sync limits in source."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    # Currency + Wi‑Fi receipts shipped in 1.15.0; later versions keep the same contracts.
    if not re.match(r"^1\.(1[5-9]|[2-9]\d)\.\d+$", version) and version != "1.15.0":
        # Accept 1.15+ (1.15.x, 1.16+, 1.2x, …)
        parts = version.split(".")
        ok = False
        try:
            major, minor = int(parts[0]), int(parts[1])
            ok = major > 1 or (major == 1 and minor >= 15)
        except (ValueError, IndexError):
            ok = False
        if not ok:
            errors.append(f"VERSION expected >= 1.15.0, got {version!r}")

    for rel in (
        "Alfa156/Alfa156/ToSettings.cs",
        "Alfa156.Android/ToSettings.cs",
        "Alfa156.Desktop/Data/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'CurrencyKey = "currency"' not in text and 'CurrencyKey = "currency"' not in text.replace(" ", ""):
            if 'internal const string CurrencyKey = "currency"' not in text:
                errors.append(f"{rel}: missing CurrencyKey")
        if "DefaultCurrency" not in text or "BYN" not in text:
            errors.append(f"{rel}: missing DefaultCurrency BYN")

    for rel, header in (
        ("Alfa156/Alfa156/SyncCrypto.cs", "KILOLEDGER2R|"),
        ("Alfa156.Android/SyncCrypto.cs", "KILOLEDGER2R|"),
    ):
        text = (ROOT / rel).read_text()
        if header not in text:
            errors.append(f"{rel}: expected protocol header {header}")
        if "1.15+" not in text:
            errors.append(f"{rel}: expected upgrade message mentioning 1.15+")

    android_receipts = (ROOT / "Alfa156.Android/ReceiptStore.cs").read_text()
    if "MaxSyncFileBytes = 2 * 1024 * 1024" not in android_receipts:
        errors.append("Android ReceiptStore: missing 2 MB cap")
    if "MaxSyncFiles = 50" not in android_receipts:
        errors.append("Android ReceiptStore: missing 50-file cap")

    kpk = (ROOT / "Alfa156/Alfa156/DataStore.cs").read_text()
    if "MaxReceiptSyncFileBytes = 2 * 1024 * 1024" not in kpk:
        errors.append("KPK DataStore: missing 2 MB receipt cap")
    if "MaxReceiptSyncFiles = 50" not in kpk:
        errors.append("KPK DataStore: missing 50 receipt cap")
    if "WriteSyncReceipts" not in kpk or "ReadSyncReceipts" not in kpk:
        errors.append("KPK DataStore: missing receipt sync send/receive")

    sync = (ROOT / "Alfa156.Android/ExpenseSyncService.cs").read_text()
    if "SendReceipts" not in sync or "ReceiveReceipts" not in sync:
        errors.append("Android ExpenseSyncService: missing receipt sync")

    # Visible BYN should be gone from UI (except currency picker help).
    for rel in (
        "Alfa156/Alfa156/Form1.cs",
        "Alfa156.Android/MainActivity.cs",
        "Alfa156.Desktop/MainForm.cs",
    ):
        text = (ROOT / rel).read_text()
        for i, line in enumerate(text.splitlines(), 1):
            if "BYN" not in line:
                continue
            if "Код валюты" in line or 'currencyBox.Text = "BYN"' in line:
                continue
            if re.search(r'"[^"]*BYN[^"]*"', line) or re.search(r"\bBYN\b", line):
                # Allow comments
                stripped = line.strip()
                if stripped.startswith("//"):
                    continue
                errors.append(f"{rel}:{i}: leftover BYN in UI: {stripped[:100]}")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK currency + receipt sync checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
