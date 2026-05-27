# Automated screenshots:
#   1. MiniPreview with a tooltip visible over a toolbar button
#   2. The Settings dialog
# Requires MiniPreview already running. Uses UI Automation + SetCursorPos.
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName WindowsBase

if (-not ("MP2.N" -as [type])) { Add-Type -Namespace MP2 -Name N -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);
[DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string title);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder sb, int n);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
public struct RECT { public int L,T,R,B; }
public delegate bool EnumProc(IntPtr h, IntPtr l);
'@ }

function Find-WindowByTitle($title) {
    $script:found = [IntPtr]::Zero
    $cb = [MP2.N+EnumProc]{
        param($h, $l)
        if (-not [MP2.N]::IsWindowVisible($h)) { return $true }
        $sb = New-Object System.Text.StringBuilder 256
        [MP2.N]::GetWindowText($h, $sb, 256) | Out-Null
        if ($sb.ToString() -eq $title) { $script:found = $h; return $false }
        return $true
    }
    [MP2.N]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:found
}

function Find-WindowByTitlePrefix($prefix, $excludeHwnd) {
    $script:found = [IntPtr]::Zero
    $script:_prefix = $prefix
    $script:_exclude = $excludeHwnd
    $cb = [MP2.N+EnumProc]{
        param($h, $l)
        if (-not [MP2.N]::IsWindowVisible($h)) { return $true }
        if ($h -eq $script:_exclude) { return $true }
        $sb = New-Object System.Text.StringBuilder 256
        [MP2.N]::GetWindowText($h, $sb, 256) | Out-Null
        $t = $sb.ToString()
        if ($t.StartsWith($script:_prefix) -and $t.Length -gt $script:_prefix.Length) { $script:found = $h; return $false }
        return $true
    }
    [MP2.N]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:found
}

function Get-Rect($hwnd) {
    $r = New-Object MP2.N+RECT
    [MP2.N]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    return $r
}

function Capture-Rect($x, $y, $w, $h, $extraTop=0, $extraBottom=0) {
    $totalH = $h + $extraTop + $extraBottom
    $bmp = New-Object System.Drawing.Bitmap $w, $totalH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, ($y - $extraTop), 0, 0, (New-Object System.Drawing.Size $w, $totalH))
    $g.Dispose()
    return $bmp
}

function Capture-Window($hwnd, $w, $h) {
    # PrintWindow API — captures the window's rendered content even if obscured.
    # PW_RENDERFULLCONTENT (2) renders the full DWM-composed content.
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [MP2.N]::PrintWindow($hwnd, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    return $bmp
}

function Compose-Polished($srcBmp, $bgPreset="blue", $padding=80, $shadowBlur=24) {
    $w = $srcBmp.Width; $h = $srcBmp.Height
    $canvasW = $w + 2 * $padding; $canvasH = $h + 2 * $padding
    $out = New-Object System.Drawing.Bitmap $canvasW, $canvasH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $presets = @{
        blue   = @([System.Drawing.Color]::FromArgb(255,40,80,160),  [System.Drawing.Color]::FromArgb(255,80,170,220))
        purple = @([System.Drawing.Color]::FromArgb(255,80,40,140),  [System.Drawing.Color]::FromArgb(255,200,90,200))
        teal   = @([System.Drawing.Color]::FromArgb(255,20,120,140), [System.Drawing.Color]::FromArgb(255,60,200,180))
        dark   = @([System.Drawing.Color]::FromArgb(255,18,22,30),   [System.Drawing.Color]::FromArgb(255,40,46,60))
    }
    $colors = $presets[$bgPreset]
    if (-not $colors) { $colors = $presets["blue"] }
    $rect = New-Object System.Drawing.Rectangle 0,0,$canvasW,$canvasH
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $colors[0], $colors[1], 135
    $g.FillRectangle($brush, $rect); $brush.Dispose()

    for ($i = $shadowBlur; $i -ge 1; $i--) {
        $alpha = [int](40 / $i)
        $sBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
        $sRect = New-Object System.Drawing.Rectangle ($padding - $i + 2), ($padding + $i + 8), ($w + 2 * $i), ($h + 2 * $i)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = 12 + $i
        $path.AddArc($sRect.X, $sRect.Y, $d, $d, 180, 90)
        $path.AddArc(($sRect.Right - $d), $sRect.Y, $d, $d, 270, 90)
        $path.AddArc(($sRect.Right - $d), ($sRect.Bottom - $d), $d, $d, 0, 90)
        $path.AddArc($sRect.X, ($sRect.Bottom - $d), $d, $d, 90, 90)
        $path.CloseFigure()
        $g.FillPath($sBrush, $path)
        $path.Dispose(); $sBrush.Dispose()
    }
    $g.DrawImage($srcBmp, $padding, $padding, $w, $h)
    $g.Dispose()
    return $out
}

$outDir = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "docs") "images"

# ---------- Screenshot 1: tooltip over a toolbar button ----------
Write-Host "[1] Tooltip screenshot..."
$mp = Find-WindowByTitle "MiniPreview"
if ($mp -eq [IntPtr]::Zero) { throw "MiniPreview not running" }
$mpRect = Get-Rect $mp
$mpW = $mpRect.R - $mpRect.L
$mpH = $mpRect.B - $mpRect.T

# Find the toolbar buttons via UI Automation
$root = [System.Windows.Automation.AutomationElement]::RootElement
$mpEl = $root.FindFirst(
    [System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "MiniPreview")))
if (-not $mpEl) { throw "UI Automation could not see MiniPreview" }

# Pick the "MuteBtn" button (well-positioned in the middle of the toolbar)
$btnCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "MuteBtn")
$btn = $mpEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCondition)
if ($btn) {
    $bRect = $btn.Current.BoundingRectangle
    $cx = [int]($bRect.X + $bRect.Width / 2)
    $cy = [int]($bRect.Y + $bRect.Height / 2)
    Write-Host "  hovering MuteBtn at ($cx, $cy)"
    [MP2.N]::SetCursorPos($cx, $cy) | Out-Null
    Start-Sleep -Milliseconds 700  # tooltip InitialShowDelay=350ms
    # Capture window + extra at bottom for tooltip below button
    $bmp = Capture-Rect $mpRect.L $mpRect.T $mpW $mpH 0 60
    $out = Compose-Polished $bmp "blue"
    $outPath = Join-Path $outDir "screenshot-tooltip.png"
    $out.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose(); $out.Dispose()
    Write-Host "  saved: $outPath"
    # Move cursor away so tooltip vanishes
    [MP2.N]::SetCursorPos(($mpRect.L - 200), $mpRect.T) | Out-Null
    Start-Sleep -Milliseconds 300
} else {
    Write-Warning "  MuteBtn not found via UI Automation; skipping tooltip shot"
}

# ---------- Screenshot 2: Settings dialog ----------
Write-Host "[2] Settings dialog screenshot..."
$settingsBtnCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "SettingsBtn")
$settingsBtn = $mpEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $settingsBtnCondition)
if ($settingsBtn) {
    $invokePattern = $settingsBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    Write-Host "  invoking SettingsBtn..."
    $invokePattern.Invoke()
    # Wait for Settings window (find any new "MiniPreview *" window != main)
    $settingsWin = [IntPtr]::Zero
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 200
        $settingsWin = Find-WindowByTitlePrefix "MiniPreview" $mp
        if ($settingsWin -ne [IntPtr]::Zero) { break }
    }
    if ($settingsWin -ne [IntPtr]::Zero) {
        $sRect = Get-Rect $settingsWin
        $sW = $sRect.R - $sRect.L; $sH = $sRect.B - $sRect.T
        Write-Host "  found settings window ${sW}x${sH} at ($($sRect.L), $($sRect.T))"
        Start-Sleep -Milliseconds 600  # let it render
        # PrintWindow grabs content even if window is obscured/offscreen.
        $bmp = Capture-Window $settingsWin $sW $sH
        $out = Compose-Polished $bmp "purple"
        $outPath = Join-Path $outDir "screenshot-settings.png"
        $out.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose(); $out.Dispose()
        Write-Host "  saved: $outPath"
        # Close it via UI Automation — find the settings AutomationElement by HWND
        $settingsEl = [System.Windows.Automation.AutomationElement]::FromHandle($settingsWin)
        if ($settingsEl) {
            $cancelCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "CancelBtn")
            $cancel = $settingsEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCondition)
            if ($cancel) {
                $p = $cancel.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                $p.Invoke()
                Write-Host "  closed via Cancel"
            }
        }
    } else {
        Write-Warning "  Settings window did not appear in time"
    }
} else {
    Write-Warning "  SettingsBtn not found"
}

# ---------- Screenshot 3: source picker dropdown open ----------
Write-Host "[3] Picker dropdown screenshot..."
Start-Sleep -Milliseconds 600  # let MP stabilise after settings close
# Re-fetch MP element — automation tree may have changed
$mpEl = $root.FindFirst(
    [System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "MiniPreview")))
$pickBtnCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "PickBtn")
$pickBtn = $mpEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $pickBtnCondition)
if ($pickBtn) {
    # Focus MP and physical click on PickBtn to open the ContextMenu.
    [MP2.N]::SetForegroundWindow($mp) | Out-Null
    Start-Sleep -Milliseconds 200
    $br = $pickBtn.Current.BoundingRectangle
    $cx = [int]($br.X + $br.Width / 2)
    $cy = [int]($br.Y + $br.Height / 2)
    [MP2.N]::SetCursorPos($cx, $cy) | Out-Null
    Start-Sleep -Milliseconds 250
    [MP2.N]::mouse_event(0x02, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [MP2.N]::mouse_event(0x04, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 1200  # window enum + render menu
    # Keep cursor near the popup so it doesn't close, but slightly off the first
    # item so no hover-highlight catches attention
    Start-Sleep -Milliseconds 200

    # Debug: confirm the menu opened by looking for Menu under root subtree
    $menusNow = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Subtree,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Menu)))
    $mpMenu = $null
    foreach ($m in $menusNow) {
        try {
            $r = $m.Current.BoundingRectangle
            # Skip menus too far from our MP window
            if ($r.Width -gt 50 -and $r.Width -lt 1200 -and $r.Height -gt 50 -and $r.Height -lt 1200) {
                $dx = [Math]::Abs($r.X - $cx)
                $dy = [Math]::Abs($r.Y - $cy)
                if ($dx -lt 800 -and $dy -lt 800) { $mpMenu = $m; break }
            }
        } catch { }
    }
    $mpRectNow = Get-Rect $mp
    if ($mpMenu) {
        $mr = $mpMenu.Current.BoundingRectangle
        Write-Host "  menu at ($([int]$mr.X),$([int]$mr.Y)) size $([int]$mr.Width)x$([int]$mr.Height)"
        $unionL = [Math]::Min($mpRectNow.L, [int]$mr.X)
        $unionT = [Math]::Min($mpRectNow.T, [int]$mr.Y)
        $unionR = [Math]::Max($mpRectNow.R, [int]($mr.X + $mr.Width))
        $unionB = [Math]::Max($mpRectNow.B, [int]($mr.Y + $mr.Height))
        $captureL = $unionL - 8
        $captureT = $unionT - 8
        $captureW = ($unionR - $unionL) + 16
        $captureH = ($unionB - $unionT) + 16
    } else {
        Write-Warning "  menu NOT detected; falling back to MP + below rect"
        $captureL = $mpRectNow.L
        $captureT = $mpRectNow.T
        $captureW = ($mpRectNow.R - $mpRectNow.L) + 200
        $captureH = ($mpRectNow.B - $mpRectNow.T) + 500
    }
    Write-Host "  capture rect: ${captureW}x${captureH} at ($captureL, $captureT)"
    $bmp = Capture-Rect $captureL $captureT $captureW $captureH 0 0
    # Trim transparent / black margins — but for now save raw with composition
    $out = Compose-Polished $bmp "dark" 50 18
    $outPath = Join-Path $outDir "screenshot-picker.png"
    $out.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose(); $out.Dispose()
    Write-Host "  saved: $outPath"

    # Dismiss popup
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
} else {
    Write-Warning "  PickBtn not found via UI Automation"
}

Write-Host "Done."
