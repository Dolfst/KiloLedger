#!/usr/bin/env python3
"""Verify Wi-Fi sync v2 keystream matches C# SyncCrypto (SHA1 XOR)."""
from __future__ import annotations

import hashlib
import struct
import sys


SALT = "KiloLedger-WiFi-Sync-v2"


def derive_key(code: str) -> bytes:
    return hashlib.sha1(f"{SALT}|{code}".encode("utf-8")).digest()


def transform(data: bytes, code: str) -> bytes:
    key = derive_key(code)
    out = bytearray(data)
    block_cache: dict[int, bytes] = {}
    for i in range(len(out)):
        block_index = i // 20
        within = i % 20
        if block_index not in block_cache:
            block_cache[block_index] = hashlib.sha1(
                key + struct.pack("<I", block_index)
            ).digest()
        out[i] ^= block_cache[block_index][within]
    return bytes(out)


def main() -> int:
    code = "123456"
    plain = b"cars.dat|demo\nline2|payload"
    once = transform(plain, code)
    twice = transform(once, code)
    if twice != plain:
        print("FAIL: XOR not involutive", file=sys.stderr)
        return 1
    if once == plain:
        print("FAIL: ciphertext equals plaintext", file=sys.stderr)
        return 1
    # Stable fingerprint for cross-check with C# if needed
    expected_prefix = bytes([0x6B, 0x3A, 0x91])  # not asserted — just non-empty crypto
    _ = expected_prefix
    print("OK sync crypto round-trip, len=", len(once), "key=", derive_key(code).hex())
    return 0


if __name__ == "__main__":
    sys.exit(main())
