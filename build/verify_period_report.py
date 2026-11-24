#!/usr/bin/env python3
"""Sanity-check printable / shareable period report (1.33.0)."""
from __future__ import annotations

import sys
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    parts = version.split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
        ok = major > 1 or (major == 1 and minor >= 33)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.33.0, got {version!r}")

    for rel in (
        "Alfa156/Alfa156/PeriodCompare.cs",
        "Alfa156.Android/PeriodCompare.cs",
        "Alfa156.Desktop/Data/PeriodCompare.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            "TopRepairLabel",
            "TopExpenseLabel",
            "TopStationLabel",
            "PersonalTripKm",
            "WorkTripKm",
            "AddTripKm",
            "AddCategoryAmount",
            "TopCategory",
            "ApplyConsumption",
            "LitersPer100Km",
            "ApplyBudget",
            "PeriodBudget",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    for rel in (
        "Alfa156.Android/PeriodReport.cs",
        "Alfa156.Desktop/Data/PeriodReport.cs",
        "Alfa156/Alfa156/PeriodReport.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            "class PeriodReport",
            "FormatText",
            "отчёт за период",
            "Топливо",
            "Ремонт",
            "Прочие",
            "Коммунальные",
            "Всего (с коммунальными)",
            "Стоимость/км",
            "Заправлено",
            "Расход",
            "Бюджет",
            "Топ ремонта",
            "Топ прочих",
            "Топ АЗС/бренд",
            "Поездки личные",
            "Поездки рабочие",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    android_report = (ROOT / "Alfa156.Android/PeriodReport.cs").read_text()
    if "FormatHtml" not in android_report:
        errors.append("Android PeriodReport: missing FormatHtml")

    desktop_report = (ROOT / "Alfa156.Desktop/Data/PeriodReport.cs").read_text()
    if "FormatHtml" not in desktop_report:
        errors.append("Desktop PeriodReport: missing FormatHtml")

    kpk_report = (ROOT / "Alfa156/Alfa156/PeriodReport.cs").read_text()
    if "namespace CarExpenses" not in kpk_report:
        errors.append("KPK PeriodReport: must use namespace CarExpenses (CF build)")
    if "Decimal.TryParse" in kpk_report:
        errors.append("KPK PeriodReport: Decimal.TryParse is not CF-safe")
    if "FormatHtml" not in kpk_report:
        errors.append("KPK PeriodReport: missing FormatHtml")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "Отчёт за период",
        "ShowPeriodReport",
        "ShowPeriodReportResult",
        "PromptCustomPeriodReport",
        "ShareSummaryHtml",
        f'version = "{version}"',
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "button.period_report",
        "ShowPeriodReport",
        "ShowPeriodReportResult",
        "PrintPeriodReport",
        "SavePeriodReportFile",
        "PrintDocument",
        f'AppVersion = "{version}"',
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")

    desktop_csproj = (ROOT / "Alfa156.Desktop/Alfa156.Desktop.csproj").read_text()
    if "PeriodReport.cs" not in desktop_csproj:
        errors.append("Desktop csproj: PeriodReport.cs not listed")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "Отчёт за период",
        "ExportPeriodReportFromMenu",
        "ExportYearReportFromMenu",
        "PeriodReport.FormatText",
        version,
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    kpk_csproj = (ROOT / "Alfa156/Alfa156/Alfa156.csproj").read_text()
    if "PeriodReport.cs" not in kpk_csproj:
        errors.append("KPK csproj: PeriodReport.cs not listed")

    build_kpk = (ROOT / "build/build-kpk.ps1").read_text()
    if "PeriodReport.cs" not in build_kpk:
        errors.append("build-kpk.ps1: PeriodReport.cs not in sources")

    android_csproj = (ROOT / "Alfa156.Android/Alfa156.Android.csproj").read_text()
    if version not in android_csproj:
        errors.append(f"Android csproj: missing ApplicationDisplayVersion {version}")

    readme = (ROOT / "README.md").read_text()
    if f"## Что нового в {version}" not in readme:
        errors.append(f"README: missing Что нового в {version}")
    if f"**{version}**" not in readme:
        errors.append(f"README: missing История {version}")

    dist_readme = (ROOT / "dist/download/README.md").read_text()
    if version not in dist_readme:
        errors.append(f"dist/download/README: missing {version}")

    # Format smoke: totals formatting must mention cost/km and car
    sample_totals_fuel = 1200.5
    sample_text = (
        "KiloLedger · отчёт за период\n"
        f"Авто: Alfa\nПериод: 2026-11-01…2026-11-30\n\n"
        f"Топливо: {sample_totals_fuel:.2f} RUB\n"
        "Ремонт: 0 RUB\nПрочие: 0 RUB\nКоммунальные: 0 RUB\n"
        f"Всего (без коммун.): {sample_totals_fuel:.2f} RUB\n"
        "Стоимость/км: н/д\nПробег: н/д"
    )
    for needle in ("отчёт за период", "Топливо", "Стоимость/км", "Авто: Alfa"):
        if needle not in sample_text:
            errors.append(f"sample report text missing {needle!r}")

    if errors:
        print("FAIL verify_period_report:")
        for e in errors:
            print(" ", e)
        return 1
    print(f"verify_period_report: OK (VERSION {version})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
