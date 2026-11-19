#!/usr/bin/env python3
"""Verify Android notification runtime permission is declared and guarded."""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Alfa156.Android" / "AndroidManifest.xml"
ACTIVITY = ROOT / "Alfa156.Android" / "MainActivity.cs"


def main() -> int:
    root = ET.parse(MANIFEST).getroot()
    android = "{http://schemas.android.com/apk/res/android}name"
    permissions = {
        item.attrib.get(android)
        for item in root.findall("uses-permission")
    }
    if "android.permission.POST_NOTIFICATIONS" not in permissions:
        print("FAIL: POST_NOTIFICATIONS permission is missing from AndroidManifest.xml")
        return 1

    source = ACTIVITY.read_text(encoding="utf-8")
    if "OperatingSystem.IsAndroidVersionAtLeast(33)" not in source:
        print("FAIL: notification permission request is not guarded for Android 13+")
        return 1

    print("OK android notification permission declaration + guard")
    return 0


if __name__ == "__main__":
    sys.exit(main())
