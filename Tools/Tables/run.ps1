param([switch]$Watch)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$candidates = @(
    $env:TABLES_PYTHON,
    "$projectRoot/.venv/Scripts/python.exe",
    (Get-Command python -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:USERPROFILE/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe"
)
$pythonPath = $null
foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate) -and $candidate -notlike '*Microsoft/WindowsApps*' -and $candidate -notlike '*Microsoft\WindowsApps*') {
        & $candidate -c 'import openpyxl' 2>$null
        if ($LASTEXITCODE -eq 0) { $pythonPath = $candidate; break }
    }
}
if (-not $pythonPath) { throw 'Install Python 3.10+ and pip install -r Tools/Tables/requirements.txt, or set TABLES_PYTHON to a configured python.exe.' }
if ($Watch) { & $pythonPath "$PSScriptRoot/build_tables.py" --watch }
else { & $pythonPath "$PSScriptRoot/build_tables.py" }
exit $LASTEXITCODE
