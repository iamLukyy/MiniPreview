# Spusti Notepad + MiniPreview a vezme screenshot
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot\..
try {
    $exe = "src/MiniPreview/bin/Release/net8.0-windows10.0.19041.0/MiniPreview.exe"
    if (-not (Test-Path $exe)) {
        Write-Host "Building..."
        dotnet build src/MiniPreview/MiniPreview.csproj -c Release -v minimal | Out-Null
    }

    Write-Host "Starting Notepad..."
    $np = Start-Process notepad.exe -PassThru
    Start-Sleep -Seconds 1
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait("MiniPreview test {DATETIME}")

    Write-Host "Starting MiniPreview..."
    $mp = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3

    Write-Host "Taking screenshot..."
    Add-Type -AssemblyName System.Drawing
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)

    $outDir = "tools/screenshots"
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }
    $ts = Get-Date -Format "yyyyMMdd-HHmmss"
    $outPath = Join-Path $outDir "verify-$ts.png"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()

    Write-Host "Screenshot saved: $outPath"
    Write-Host "Cleaning up..."
    try { $mp.Kill() } catch {}
    try { $np.Kill() } catch {}
} finally {
    Pop-Location
}
