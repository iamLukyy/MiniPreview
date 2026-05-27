# Spusti --selftest a overi JSON output
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot\..
try {
    Write-Host "Building..."
    dotnet build src/MiniPreview/MiniPreview.csproj -c Release -v minimal | Out-Null

    $exe = "src/MiniPreview/bin/Release/net8.0-windows10.0.19041.0/MiniPreview.exe"
    if (-not (Test-Path $exe)) { throw "Build did not produce $exe" }

    Write-Host "Running --selftest..."
    $jsonOutput = & $exe --selftest
    $exitCode = $LASTEXITCODE

    Write-Host $jsonOutput
    $report = $jsonOutput | ConvertFrom-Json

    if (-not $report.probesOk) { throw "Probes failed" }
    if (-not $report.captureOk) { throw "Capture failed: $($report.captureError)" }
    if (-not $report.audioOk)   { throw "Audio failed: $($report.audioError)" }
    if ($report.framesPerSec -lt 1) { throw "FPS too low: $($report.framesPerSec)" }

    Write-Host "Smoke test PASS (exit=$exitCode, fps=$($report.framesPerSec))"
    exit 0
} finally {
    Pop-Location
}
