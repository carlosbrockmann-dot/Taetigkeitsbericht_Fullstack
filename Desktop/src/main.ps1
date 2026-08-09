$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$activateScript = Join-Path $repoRoot ".venv\Scripts\Activate.ps1"
$pythonExe = Join-Path $repoRoot ".venv\Scripts\python.exe"

if (-not (Test-Path $activateScript)) {
    throw "Virtuelle Umgebung nicht gefunden: $activateScript"
}
if (-not (Test-Path $pythonExe)) {
    throw "Python in virtueller Umgebung nicht gefunden: $pythonExe"
}

. $activateScript

try {
    Push-Location $repoRoot
    & $pythonExe "$scriptDir\main.py"
}
finally {
    Pop-Location
    if (Get-Command deactivate -ErrorAction SilentlyContinue) {
        deactivate
    }
}
