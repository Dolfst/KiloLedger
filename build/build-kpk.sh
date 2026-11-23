#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/KiloLedger/KiloLedger"
CF_DIR="$ROOT/build/cf35/WindowsCE"
CONFIG="${1:-Release}"
OUT_DIR="${2:-$PROJECT/bin/$CONFIG}"
OBJ_DIR="$PROJECT/obj/$CONFIG"

case "$CONFIG" in
  Debug|Release) ;;
  *) echo "Configuration must be Debug or Release" >&2; exit 1 ;;
esac

if [[ "$(uname -s)" == "Darwin" ]]; then
  MCS="${MCS:-mcs}"
  RESGEN="${RESGEN:-resgen}"
else
  MCS="${MCS:-mcs}"
  RESGEN="${RESGEN:-resgen}"
fi

if ! command -v "$MCS" >/dev/null 2>&1; then
  echo "mcs is not installed (install the mono-devel package)" >&2
  exit 1
fi
if ! command -v "$RESGEN" >/dev/null 2>&1; then
  echo "resgen is not installed (install the mono-devel package)" >&2
  exit 1
fi

REF_NAMES=(
  mscorlib.dll System.dll System.Core.dll System.Data.dll
  System.Drawing.dll System.Windows.Forms.dll System.Xml.dll
  System.Xml.Linq.dll Microsoft.Windowsce.Forms.dll
)
for ref in "${REF_NAMES[@]}"; do
  if [[ ! -f "$CF_DIR/$ref" ]]; then
    echo "Missing CF reference: $CF_DIR/$ref" >&2
    exit 1
  fi
done

mkdir -p "$OUT_DIR" "$OBJ_DIR"

"$RESGEN" "$PROJECT/Form1.resx" "$OBJ_DIR/CarExpenses.Form1.resources"
"$RESGEN" "$PROJECT/Properties/Resources.resx" "$OBJ_DIR/KiloLedger.Properties.Resources.resources"

SOURCES=(
  "$PROJECT/Form1.cs"
  "$PROJECT/Form1.Designer.cs"
  "$PROJECT/CarsMeta.cs"
  "$PROJECT/RecordTemplates.cs"
  "$PROJECT/DateReminders.cs"
  "$PROJECT/CarNotes.cs"
  "$PROJECT/DataStore.cs"
  "$PROJECT/EntryEditorForm.cs"
  "$PROJECT/RecordIds.cs"
  "$PROJECT/DuplicateFinder.cs"
  "$PROJECT/CarCompare.cs"
  "$PROJECT/PeriodCompare.cs"
  "$PROJECT/PeriodReport.cs"
  "$PROJECT/ForecastInsights.cs"
  "$PROJECT/DataIntegrity.cs"
  "$PROJECT/ToSettings.cs"
  "$PROJECT/SyncCrypto.cs"
  "$PROJECT/SyncMerge.cs"
  "$PROJECT/SyncOptionsForm.cs"
  "$PROJECT/Program.cs"
  "$PROJECT/Localization.cs"
  "$PROJECT/Properties/AssemblyInfo.cs"
  "$PROJECT/Properties/Resources.Designer.cs"
)

ARGS=(
  -nostdlib
  -noconfig
  -target:winexe
  -platform:anycpu
  "-out:$OUT_DIR/KiloLedger.exe"
  "-win32icon:$PROJECT/KiloLedger.ico"
  "-resource:$OBJ_DIR/CarExpenses.Form1.resources,CarExpenses.Form1.resources"
  "-resource:$OBJ_DIR/KiloLedger.Properties.Resources.resources,KiloLedger.Properties.Resources.resources"
  "-resource:$PROJECT/SeedData/Repairs.seed,KiloLedger.SeedData.Repairs.seed"
  "-resource:$PROJECT/SeedData/Utilities.seed,KiloLedger.SeedData.Utilities.seed"
  "-resource:$PROJECT/SeedData/Vehicles.seed,KiloLedger.SeedData.Vehicles.seed"
  "-resource:$PROJECT/resources/strings.ru.json,strings.ru.json"
  "-resource:$PROJECT/resources/strings.en.json,strings.en.json"
  "-resource:$PROJECT/resources/strings.es.json,strings.es.json"
  "-resource:$PROJECT/resources/strings.fr.json,strings.fr.json"
  "-resource:$PROJECT/resources/strings.de.json,strings.de.json"
)

if [[ "$CONFIG" == "Debug" ]]; then
  ARGS+=("-debug" "-define:DEBUG;TRACE;PocketPC")
else
  ARGS+=("-optimize+" "-define:TRACE;PocketPC")
fi

for ref in "${REF_NAMES[@]}"; do
  ARGS+=("-r:$CF_DIR/$ref")
done

"$MCS" "${ARGS[@]}" "${SOURCES[@]}"

echo "Built $OUT_DIR/KiloLedger.exe"
