# Builds Alfa156.exe for Windows Mobile (.NET CF 3.5) without full WM SDK.
# Uses desktop csc against CF reference assemblies in build/cf35/WindowsCE.
param(
    [string]$Configuration = "Release",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projDir = Join-Path $root "Alfa156\Alfa156"
$cfDir = Join-Path $root "build\cf35\WindowsCE"
if (-not (Test-Path (Join-Path $projDir "Alfa156.csproj"))) {
    throw "Project not found: $projDir"
}
if ($OutDir -eq "") {
    $OutDir = Join-Path $projDir "bin\$Configuration"
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$objDir = Join-Path $projDir "obj\$Configuration"
New-Item -ItemType Directory -Path $objDir -Force | Out-Null

$csc = @(
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw "csc.exe not found" }

$resgen = @(
    "${env:ProgramFiles}\Microsoft SDKs\Windows\v6.0A\Bin\ResGen.exe",
    "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\ResGen.exe",
    "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\ResGen.exe",
    "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\ResGen.exe",
    "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\ResGen.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $resgen) {
    $resgen = Get-ChildItem @("${env:ProgramFiles}\Microsoft SDKs", "${env:ProgramFiles(x86)}\Microsoft SDKs", "${env:ProgramFiles(x86)}\Microsoft Visual Studio") `
        -Recurse -Filter ResGen.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $resgen) { throw "ResGen.exe not found (install .NET Framework Developer Pack / Windows SDK)" }

$refNames = @(
    "mscorlib.dll", "System.dll", "System.Core.dll", "System.Data.dll",
    "System.Drawing.dll", "System.Windows.Forms.dll", "System.Xml.dll",
    "System.Xml.Linq.dll", "Microsoft.Windowsce.Forms.dll"
)
$refs = @()
foreach ($name in $refNames) {
    $p = Join-Path $cfDir $name
    if (-not (Test-Path $p)) { throw "Missing CF ref: $p" }
    $refs += $p
}

$formResx = Join-Path $projDir "Form1.resx"
$formResources = Join-Path $objDir "CarExpenses.Form1.resources"
$rg1 = Start-Process -FilePath $resgen -ArgumentList "`"$formResx`" `"$formResources`"" -Wait -PassThru -NoNewWindow
if ($rg1.ExitCode -ne 0) { throw "ResGen Form1.resx failed" }

$propResx = Join-Path $projDir "Properties\Resources.resx"
$propResources = Join-Path $objDir "Alfa156.Properties.Resources.resources"
$rg2 = Start-Process -FilePath $resgen -ArgumentList "`"$propResx`" `"$propResources`"" -Wait -PassThru -NoNewWindow
if ($rg2.ExitCode -ne 0) { throw "ResGen Resources.resx failed" }

$sources = @(
    (Join-Path $projDir "Form1.cs"),
    (Join-Path $projDir "Form1.Designer.cs"),
    (Join-Path $projDir "DataStore.cs"),
    (Join-Path $projDir "EntryEditorForm.cs"),
    (Join-Path $projDir "RecordIds.cs"),
    (Join-Path $projDir "DuplicateFinder.cs"),
    (Join-Path $projDir "ToSettings.cs"),
    (Join-Path $projDir "SyncCrypto.cs"),
    (Join-Path $projDir "SyncMerge.cs"),
    (Join-Path $projDir "SyncOptionsForm.cs"),
    (Join-Path $projDir "Program.cs"),
    (Join-Path $projDir "Properties\AssemblyInfo.cs"),
    (Join-Path $projDir "Properties\Resources.Designer.cs")
)

$outExe = Join-Path $OutDir "Alfa156.exe"
$icon = Join-Path $projDir "Alfa156.ico"
$rsp = Join-Path $objDir "Alfa156.csc.rsp"
$define = "TRACE;PocketPC"
if ($Configuration -eq "Debug") { $define = "DEBUG;TRACE;PocketPC" }

function Q([string]$path) {
    if ($path -match '\s') { return '"' + $path + '"' }
    return $path
}
function QRes([string]$path, [string]$name) {
    return "/resource:" + (Q $path) + "," + $name
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("/nologo")
$lines.Add("/nostdlib+")
$lines.Add("/target:winexe")
$lines.Add("/platform:anycpu")
$lines.Add("/optimize+")
$lines.Add("/define:$define")
$lines.Add("/out:" + (Q $outExe))
$lines.Add("/win32icon:" + (Q $icon))
$lines.Add((QRes $formResources "CarExpenses.Form1.resources"))
$lines.Add((QRes $propResources "Alfa156.Properties.Resources.resources"))
$lines.Add((QRes (Join-Path $projDir "SeedData\Repairs.seed") "Alfa156.SeedData.Repairs.seed"))
$lines.Add((QRes (Join-Path $projDir "SeedData\Utilities.seed") "Alfa156.SeedData.Utilities.seed"))
$lines.Add((QRes (Join-Path $projDir "SeedData\Vehicles.seed") "Alfa156.SeedData.Vehicles.seed"))
foreach ($r in $refs) { $lines.Add("/r:" + (Q $r)) }
foreach ($s in $sources) { $lines.Add((Q $s)) }
[System.IO.File]::WriteAllLines($rsp, $lines)

Write-Host "csc: $csc"
Write-Host "rsp: $rsp"
# /noconfig must be on the command line (ignored inside rsp) so desktop refs are not pulled in.
$p = Start-Process -FilePath $csc -ArgumentList "/noconfig `"@$rsp`"" -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0) { throw "KPK build failed with exit $($p.ExitCode)" }

Write-Host "Built $outExe"
Get-Item $outExe | Format-List FullName, Length, LastWriteTime
