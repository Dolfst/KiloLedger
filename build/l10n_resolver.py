#!/usr/bin/env python3
"""Resolve L10n.Get/L10n.Format calls in C# source files against strings.ru.json for smoke checks."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RU_PATH = ROOT / "localization" / "resources" / "strings.ru.json"

_INSTALLED = False


def _escape_cs_literal(value: str) -> str:
    return (
        value.replace("\\", "\\\\")
        .replace('"', '\\"')
        .replace("\r", "\\r")
        .replace("\n", "\\n")
    )


def resolve_cs_text(text: str, ru_map: dict[str, str]) -> str:
    def repl_get(match: re.Match[str]) -> str:
        key = match.group(1)
        val = _escape_cs_literal(ru_map.get(key, key))
        return f'"{val}"'

    def repl_fmt(match: re.Match[str]) -> str:
        key = match.group(1)
        val = _escape_cs_literal(ru_map.get(key, key))
        return f'string.Format("{val}",'

    text = re.sub(r'L10n\.Get\("([^"]+)"\)', repl_get, text)
    text = re.sub(r'L10n\.Format\("([^"]+)"\s*,', repl_fmt, text)
    return text


def install() -> None:
    global _INSTALLED
    if _INSTALLED or not RU_PATH.is_file():
        return
    ru_map = json.loads(RU_PATH.read_text(encoding="utf-8"))
    orig_read_text = Path.read_text

    def patched_read_text(self: Path, *args: object, **kwargs: object) -> str:
        content = orig_read_text(self, *args, **kwargs)
        if self.suffix.lower() == ".cs":
            return resolve_cs_text(content, ru_map)
        return content

    Path.read_text = patched_read_text  # type: ignore[method-assign]
    _INSTALLED = True
