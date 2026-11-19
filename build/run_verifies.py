#!/usr/bin/env python3
"""Run all build/verify_*.py smoke checks. Exit non-zero on first failure."""
from __future__ import annotations

import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "build"


def main() -> int:
    scripts = sorted(BUILD.glob("verify_*.py"))
    if not scripts:
        print("No verify_*.py scripts found", file=sys.stderr)
        return 1
    started = time.time()
    failed = 0
    for script in scripts:
        print(f"—— {script.name} ——")
        proc = subprocess.run([sys.executable, str(script)], cwd=str(ROOT))
        if proc.returncode != 0:
            print(f"FAILED: {script.name} (exit {proc.returncode})", file=sys.stderr)
            failed += 1
    elapsed = time.time() - started
    print(f"—— done: {len(scripts) - failed}/{len(scripts)} ok in {elapsed:.1f}s ——")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
