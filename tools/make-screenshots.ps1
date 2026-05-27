param(
    [string]$Name = "screenshot",
    [string]$Bg   = "blue",
    [int]$Padding = 80,
    [int]$ShadowBlur = 24
)

# Captures the running MiniPreview window, drops it on a polished gradient
# background with a soft shadow, and saves polished PNG(s) to docs/images/.
#
# Usage: launch MiniPreview, get it into the state you want pictured, then run
#   .\tools\make-screenshots.ps1                  # one shot
#   .\tools\make-screenshots.ps1 -Name hero       # custom output name
#   .\tools\make-screenshots.ps1 -Bg "purple"     # one of: blue, purple, teal, dark
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# --- Find MiniPreview window ---
Add-Type -Namespace MP -Name N -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string title);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder sb, int n);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
public struct RECT { public int L,T,R,B; }
public delegate bool EnumProc(IntPtr h, IntPtr l);
'@

$hwnd = [IntPtr]::Zero
$cb = [MP.N+EnumProc]{
    param($h, $l)
    if (-not [MP.N]::IsWindowVisible($h)) { return $true }
    $sb = New-Object System.Text.StringBuilder 256
    [MP.N]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString() -eq "MiniPreview") {
        $script:hwnd = $h
        return $false
    }
    return $true
}
[MP.N]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

if ($hwnd -eq [IntPtr]::Zero) {
    throw "MiniPreview window not found. Launch the app first."
}

$rect = New-Object MP.N+RECT
[MP.N]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.R - $rect.L
$h = $rect.B - $rect.T
Write-Host "Found MiniPreview window: ${w}x${h} at ($($rect.L), $($rect.T))"

# --- Grab pixels for that rect ---
$src = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($src)
$g.CopyFromScreen($rect.L, $rect.T, 0, 0, (New-Object System.Drawing.Size $w, $h))
$g.Dispose()

# --- Build polished composite ---
$canvasW = $w + 2 * $Padding
$canvasH = $h + 2 * $Padding
$out = New-Object System.Drawing.Bitmap $canvasW, $canvasH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g2 = [System.Drawing.Graphics]::FromImage($out)
$g2.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g2.InterpolationMode= [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# Gradient background
$presets = @{
    blue   = @([System.Drawing.Color]::FromArgb(255,40,80,160),  [System.Drawing.Color]::FromArgb(255,80,170,220))
    purple = @([System.Drawing.Color]::FromArgb(255,80,40,140),  [System.Drawing.Color]::FromArgb(255,200,90,200))
    teal   = @([System.Drawing.Color]::FromArgb(255,20,120,140), [System.Drawing.Color]::FromArgb(255,60,200,180))
    dark   = @([System.Drawing.Color]::FromArgb(255,18,22,30),   [System.Drawing.Color]::FromArgb(255,40,46,60))
}
$colors = $presets[$Bg]
if (-not $colors) { $colors = $presets["blue"] }
$canvasRect = New-Object System.Drawing.Rectangle 0,0,$canvasW,$canvasH
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $canvasRect, $colors[0], $colors[1], 135
$g2.FillRectangle($brush, $canvasRect)
$brush.Dispose()

# Soft shadow under the window: stack of translucent black rounded rects
for ($i = $ShadowBlur; $i -ge 1; $i--) {
    $alpha = [int](40 / $i)
    $shadowColor = [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0)
    $sBrush = New-Object System.Drawing.SolidBrush $shadowColor
    $sRect = New-Object System.Drawing.Rectangle ($Padding - $i + 2), ($Padding + $i + 8), ($w + 2 * $i), ($h + 2 * $i)
    # Approximate rounded rect via path
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 12 + $i
    $path.AddArc($sRect.X, $sRect.Y, $d, $d, 180, 90)
    $path.AddArc(($sRect.Right - $d), $sRect.Y, $d, $d, 270, 90)
    $path.AddArc(($sRect.Right - $d), ($sRect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($sRect.X, ($sRect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    $g2.FillPath($sBrush, $path)
    $path.Dispose(); $sBrush.Dispose()
}

# The actual window screenshot, centered
$g2.DrawImage($src, $Padding, $Padding, $w, $h)
$g2.Dispose()
$src.Dispose()

# --- Save ---
$outDir = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "docs") "images"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$outPath = Join-Path $outDir "$Name.png"
$out.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Write-Host "Saved: $outPath ($([int]((Get-Item $outPath).Length / 1024)) KB)"
