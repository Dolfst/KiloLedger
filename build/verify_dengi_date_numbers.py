#!/usr/bin/env python3
"""Date-formatted numeric cells must become day.month decimals, not Excel serials.

27.03 (day 27, month 3) -> 27.3.  05.10 (day 5, month 10) -> 5.1.
Serial 46108 is 2026-03-27. A time or an unreadable date is not stored as the serial.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESKTOP = ROOT / "KiloLedger.Desktop/Exchange/DengiDateNumbers.cs"
ANDROID = ROOT / "KiloLedger.Android/DengiDateNumbers.cs"
GS = ROOT / "google-sync/KiloLedgerSync.gs"
STRING_ROOTS = (
    "localization/resources",
    "KiloLedger.Desktop/resources",
    "KiloLedger.Android/Assets",
    "KiloLedger/KiloLedger/resources",
)
LANGS = ("ru", "en", "es", "fr", "de")

# text, date_formatted, kind, number
CASES = [
    ("46108", True, "Recovered", "27.3"),
    ("46108.0", True, "Recovered", "27.3"),
    ("46300", True, "Recovered", "5.1"),
    ("46027", True, "Recovered", "5.1"),
    ("46108.5", True, "Ambiguous", None),
    ("46108", False, "NotDate", None),
    ("45000", False, "NotDate", None),
    ("27.3", False, "NotDate", None),
    ("27.03", False, "NotDate", None),
    ("05.10", False, "NotDate", None),
    ("27.3", True, "NotDate", None),
    ("27.03.2026", False, "Recovered", "27.3"),
    ("05.10.2026", False, "Recovered", "5.1"),
    ("27.3.2026", False, "Recovered", "27.3"),
    ("2026-03-27", False, "Recovered", "27.3"),
    ("2026-03-27T00:00:00Z", False, "Recovered", "27.3"),
    ("2026-10-05T00:00:00", False, "Recovered", "5.1"),
    ("2026-03-27T21:00:00Z", False, "Ambiguous", None),
    ("27.03.2026 13:40", False, "Ambiguous", None),
    ("31.02.2026", False, "Ambiguous", None),
    ("27.03.1970", False, "Ambiguous", None),
    ("#REF!", True, "NotDate", None),
]


def main() -> int:
    errors: list[str] = []
    errors.extend(check_sources())
    errors.extend(check_strings())
    errors.extend(run_csharp())
    errors.extend(run_javascript())
    if errors:
        print("FAIL verify_dengi_date_numbers:")
        for error in errors:
            print(" ", error)
        return 1
    print("verify_dengi_date_numbers: OK")
    return 0


def check_sources() -> list[str]:
    errors: list[str] = []
    desktop = DESKTOP.read_text(encoding="utf-8")
    android = ANDROID.read_text(encoding="utf-8")
    if desktop.replace("namespace KiloLedger.Desktop.Exchange", "namespace KiloLedger.Android") != android:
        errors.append("Android DengiDateNumbers.cs drifted from the Desktop copy")
    dengi = (ROOT / "KiloLedger.Desktop/Exchange/DengiFormat.cs").read_text(encoding="utf-8")
    for token in (
        "DengiDateNumbers.Classify",
        "DateNumberKind.Ambiguous",
        "DateNumberKind.Recovered",
        "import.date_number_skipped",
        "CleanMeasure(CellAt(src, 1))",
        "CleanMeasure(CellAt(src, 2))",
        "CleanMeasure(CellAt(src, 3))",
        "CleanMeasure(CellAt(src, 5))",
    ):
        if token not in dengi:
            errors.append(f"DengiFormat.cs: missing {token}")
    xlsx = (ROOT / "KiloLedger.Desktop/Exchange/XlsxExchange.cs").read_text(encoding="utf-8")
    for token in (
        "ReadDateStyleIndexes",
        "DengiDateNumbers.IsDayMonthFormat",
        "new DengiFormat.SheetCell",
        'String.Equals(type, "d"',
    ):
        if token not in xlsx:
            errors.append(f"XlsxExchange.cs: missing {token}")
    preview = (ROOT / "KiloLedger.Desktop/Exchange/ImportPreview.cs").read_text(encoding="utf-8")
    if "DengiFormat.DateNumberWarnings" not in preview or "import.date_number_warnings" not in preview:
        errors.append("ImportPreview does not show date-number warnings")
    sync = (ROOT / "KiloLedger.Desktop/Exchange/GoogleSyncClient.cs").read_text(encoding="utf-8")
    android_sync = (ROOT / "KiloLedger.Android/GoogleSyncService.cs").read_text(encoding="utf-8")
    for label, text in (("Desktop sync", sync), ("Android sync", android_sync)):
        if "DengiDateNumbers.Classify" not in text:
            errors.append(f"{label}: numeric fields are not passed through DengiDateNumbers")
        if "dateNumberWarnings" not in text:
            errors.append(f"{label}: script warnings are not read")
        if "import.date_number_skipped" not in text:
            errors.append(f"{label}: ambiguous values are not reported")
    gs = GS.read_text(encoding="utf-8")
    for token in (
        "function classifyMeasure_",
        "[object Date]",
        "function formatDayMonth_",
        "dateNumberWarnings_",
        "isDayMonthFormat_",
        "function measure_",
    ):
        if token not in gs:
            errors.append(f"KiloLedgerSync.gs: missing {token}")
    if "num_(display[r][3])" in gs or "num_(display[r][6])" in gs:
        errors.append("KiloLedgerSync.gs still reads a money column only through num_")
    return errors


def check_strings() -> list[str]:
    errors: list[str] = []
    for folder in STRING_ROOTS:
        for lang in LANGS:
            path = ROOT / folder / f"strings.{lang}.json"
            if not path.is_file():
                errors.append(f"missing {path.relative_to(ROOT)}")
                continue
            data = json.loads(path.read_text(encoding="utf-8"))
            header = data.get("import.date_number_warnings", "")
            detail = data.get("import.date_number_skipped", "")
            if not header:
                errors.append(f"{path.relative_to(ROOT)}: missing import.date_number_warnings")
            if "{0}" not in detail:
                errors.append(f"{path.relative_to(ROOT)}: import.date_number_skipped must include {{0}}")
    return errors


def run_csharp() -> list[str]:
    mcs = shutil.which("mcs")
    mono = shutil.which("mono")
    if not mcs or not mono:
        return ["mcs/mono not available; cannot execute DengiDateNumbers"]
    errors: list[str] = []
    with tempfile.TemporaryDirectory(prefix="dengi-date-") as tmp:
        work = Path(tmp)
        program = work / "DateNumberCheck.cs"
        program.write_text(csharp_driver(), encoding="utf-8")
        exe = work / "dengi-date.exe"
        compile = subprocess.run(
            [mcs, "-codepage:utf8", "-sdk:4.5", f"-out:{exe}", str(DESKTOP), str(program)],
            capture_output=True,
            text=True,
        )
        if compile.returncode != 0:
            return ["mcs failed: " + (compile.stderr or compile.stdout).strip()]
        run = subprocess.run([mono, str(exe)], capture_output=True, text=True)
        if run.returncode != 0:
            errors.append("DengiDateNumbers cases failed:\n" + (run.stdout or run.stderr))
    return errors


def csharp_driver() -> str:
    lines = [
        "using System;",
        "namespace KiloLedger.Desktop.Exchange",
        "{",
        "    internal static class DateNumberCheck",
        "    {",
        "        private static int fails;",
        "        public static int Main()",
        "        {",
    ]
    for text, flagged, kind, number in CASES:
        number_arg = "null" if number is None else '"' + number + '"'
        flag = "true" if flagged else "false"
        escaped = text.replace("\\", "\\\\").replace('"', '\\"')
        lines.append(
            f'            Expect("{escaped}", {flag}, DateNumberKind.{kind}, {number_arg});'
        )
    lines.extend(
        [
            '            if (!DengiDateNumbers.IsDayMonthFormat("dd.mm.yyyy")) Fail("fmt date");',
            '            if (!DengiDateNumbers.IsDayMonthFormat("dd\\".\\"mm\\".\\"yyyy")) Fail("fmt quoted");',
            '            if (DengiDateNumbers.IsDayMonthFormat("0.00")) Fail("fmt number");',
            '            if (DengiDateNumbers.IsDayMonthFormat("h:mm")) Fail("fmt time");',
            '            if (DengiDateNumbers.FormatDayMonth(27, 3) != "27.3") Fail("27.03");',
            '            if (DengiDateNumbers.FormatDayMonth(5, 10) != "5.1") Fail("05.10");',
            '            if (DengiDateNumbers.FormatDayMonth(15, 12) != "15.12") Fail("15.12");',
            "            if (fails != 0)",
            "            {",
            '                Console.WriteLine("FAIL " + fails);',
            "                return 1;",
            "            }",
            '            Console.WriteLine("OK");',
            "            return 0;",
            "        }",
            "        private static void Expect(string text, bool dateFormatted, DateNumberKind kind, string number)",
            "        {",
            "            string got;",
            "            DateNumberKind actual = DengiDateNumbers.Classify(text, dateFormatted, out got);",
            "            if (actual != kind || (kind == DateNumberKind.Recovered && got != number))",
            "            {",
            '                fails++;',
            '                Console.WriteLine("FAIL [" + text + "] -> " + actual + " " + got);',
            "            }",
            "        }",
            "        private static void Fail(string message)",
            "        {",
            "            fails++;",
            '            Console.WriteLine("FAIL " + message);',
            "        }",
            "    }",
            "}",
            "",
        ]
    )
    return "\n".join(lines)


def run_javascript() -> list[str]:
    node = shutil.which("node") or shutil.which("nodejs")
    if not node:
        return ["node not available; cannot execute classifyMeasure_"]
    gs = GS.read_text(encoding="utf-8")
    start = gs.find("// BEGIN dengi-date-number")
    end = gs.find("// END dengi-date-number")
    if start < 0 or end < 0:
        return ["KiloLedgerSync.gs: missing dengi-date-number block"]
    block = gs[start:end]
    script = block + "\n" + js_driver()
    run = subprocess.run([node, "-e", script], capture_output=True, text=True)
    if run.returncode != 0:
        return ["classifyMeasure_ cases failed:\n" + (run.stdout or "") + (run.stderr or "")]
    return []


def js_driver() -> str:
    lines = [
        "var fails = 0;",
        "function expect(value, display, format, kind, text) {",
        "  var got = classifyMeasure_(value, display, format);",
        "  if (got.kind !== kind || (kind === 'recovered' && got.text !== text)) {",
        "    fails++;",
        "    console.log('FAIL', display, value, got);",
        "  }",
        "}",
        "function dayMonth(day, month) {",
        "  var scale = month >= 10 ? 100 : 10;",
        "  var text = String(day + month / scale);",
        "  if (text !== formatDayMonth_(day, month)) { fails++; console.log('FAIL format', day, month, text); }",
        "}",
    ]
    lines.append('expect(46108, "46108", "dd.mm.yyyy", "recovered", "27.3");')
    lines.append('expect(46300, "46300", "dd.mm.yyyy", "recovered", "5.1");')
    lines.append('expect(46027, "46027", "dd.mm.yyyy", "recovered", "5.1");')
    lines.append('expect(46108.5, "46108.5", "dd.mm.yyyy", "ambiguous", "");')
    lines.append('expect(46108, "46108", "0.00", "not-date", "");')
    lines.append('expect(45000, "45000", "General", "not-date", "");')
    lines.append('expect(27.3, "27.3", "0.00", "not-date", "");')
    lines.append('expect("27.03.2026", "27.03.2026", "General", "recovered", "27.3");')
    lines.append('expect("05.10.2026", "05.10.2026", "@", "recovered", "5.1");')
    lines.append('expect("2026-03-27T00:00:00Z", "2026-03-27T00:00:00Z", "@", "recovered", "27.3");')
    lines.append('expect("2026-03-27T21:00:00Z", "2026-03-27T21:00:00Z", "@", "ambiguous", "");')
    lines.append('expect("27.03.2026 13:40", "27.03.2026 13:40", "@", "ambiguous", "");')
    lines.append('expect("31.02.2026", "31.02.2026", "@", "ambiguous", "");')
    lines.append('var march = new Date(2026, 2, 27, 0, 0, 0);')
    lines.append('expect(march, "27.03.2026", "dd.mm.yyyy", "recovered", "27.3");')
    lines.append('var october = new Date(2026, 9, 5, 0, 0, 0);')
    lines.append('expect(october, "05.10.2026", "dd.mm.yyyy", "recovered", "5.1");')
    lines.append('var timed = new Date(2026, 2, 27, 13, 40, 0);')
    lines.append('expect(timed, "27.03.2026 13:40", "dd.mm.yyyy", "ambiguous", "");')
    lines.append('if (!isDayMonthFormat_("dd.mm.yyyy")) { fails++; console.log("FAIL fmt"); }')
    lines.append('if (isDayMonthFormat_("0.00")) { fails++; console.log("FAIL numfmt"); }')
    lines.append('if (isDayMonthFormat_("h:mm")) { fails++; console.log("FAIL timefmt"); }')
    lines.append("for (var month = 1; month <= 12; month++) for (var day = 1; day <= 28; day++) dayMonth(day, month);")
    lines.append('if (formatDayMonth_(27, 3) !== "27.3") { fails++; console.log("FAIL 27.3"); }')
    lines.append('if (formatDayMonth_(5, 10) !== "5.1") { fails++; console.log("FAIL 5.1"); }')
    lines.append("if (fails) process.exit(1);")
    lines.append('console.log("OK");')
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    sys.exit(main())
