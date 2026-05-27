# Generates MiniPreview.ico (multi-resolution 16/32/48/64/128/256)
# Design: dark borderless window with a teal "live preview" thumbnail inside + white play triangle
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$outPath = Join-Path $PSScriptRoot "..\src\MiniPreview\Resources\MiniPreview.ico"
$sizes = @(16, 32, 48, 64, 128, 256)

function New-Bitmap {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 256.0

    # Outer rounded frame (the floating preview window)
    $framePad = [int](16 * $scale)
    $frameRect = New-Object System.Drawing.Rectangle $framePad, $framePad, ($size - 2 * $framePad), ($size - 2 * $framePad)
    $cornerRadius = [Math]::Max(2, [int](20 * $scale))

    # Build rounded rectangle path
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $cornerRadius * 2
    $path.AddArc($frameRect.X, $frameRect.Y, $d, $d, 180, 90)
    $path.AddArc(($frameRect.Right - $d), $frameRect.Y, $d, $d, 270, 90)
    $path.AddArc(($frameRect.Right - $d), ($frameRect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($frameRect.X, ($frameRect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()

    # Fill outer frame (dark)
    $frameBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 35, 45))
    $g.FillPath($frameBrush, $path)

    # Inner preview thumbnail (teal/cyan gradient)
    $innerPad = [int](14 * $scale)
    $inner = New-Object System.Drawing.Rectangle ($frameRect.X + $innerPad), ($frameRect.Y + $innerPad), ($frameRect.Width - 2 * $innerPad), ($frameRect.Height - 2 * $innerPad)
    if ($inner.Width -gt 0 -and $inner.Height -gt 0) {
        $gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $inner, ([System.Drawing.Color]::FromArgb(255, 60, 180, 200)), ([System.Drawing.Color]::FromArgb(255, 30, 130, 180)), 45
        $g.FillRectangle($gradBrush, $inner)
        $gradBrush.Dispose()

        # Play triangle in middle
        $cx = $size / 2.0
        $cy = $size / 2.0
        $triSize = [Math]::Max(4, 36 * $scale)
        $pts = New-Object 'System.Drawing.PointF[]' 3
        $pts[0] = New-Object System.Drawing.PointF ($cx - $triSize * 0.4), ($cy - $triSize * 0.55)
        $pts[1] = New-Object System.Drawing.PointF ($cx + $triSize * 0.6), $cy
        $pts[2] = New-Object System.Drawing.PointF ($cx - $triSize * 0.4), ($cy + $triSize * 0.55)
        $playBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(240, 255, 255, 255))
        $g.FillPolygon($playBrush, $pts)
        $playBrush.Dispose()
    }

    # Thin white outline around outer frame (so it's visible on dark backgrounds)
    $outlinePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180, 255, 255, 255)), ([Math]::Max(1, $scale * 2))
    $g.DrawPath($outlinePen, $path)
    $outlinePen.Dispose()

    $frameBrush.Dispose()
    $path.Dispose()
    $g.Dispose()
    return $bmp
}

function Write-Ico {
    param([string]$Path, [System.Drawing.Bitmap[]]$Bitmaps)

    # Encode each as PNG
    $pngBytes = @()
    foreach ($b in $Bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes += ,$ms.ToArray()
        $ms.Dispose()
    }

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        # ICONDIR header
        $writer.Write([UInt16]0)                  # reserved
        $writer.Write([UInt16]1)                  # type = ICO
        $writer.Write([UInt16]$Bitmaps.Length)    # image count

        $offset = 6 + 16 * $Bitmaps.Length
        for ($i = 0; $i -lt $Bitmaps.Length; $i++) {
            $b = $Bitmaps[$i]
            $w = if ($b.Width -ge 256) { [byte]0 } else { [byte]$b.Width }
            $h = if ($b.Height -ge 256) { [byte]0 } else { [byte]$b.Height }
            $writer.Write([byte]$w)
            $writer.Write([byte]$h)
            $writer.Write([byte]0)                # palette count
            $writer.Write([byte]0)                # reserved
            $writer.Write([UInt16]1)              # color planes
            $writer.Write([UInt16]32)             # bits per pixel
            $writer.Write([UInt32]$pngBytes[$i].Length)
            $writer.Write([UInt32]$offset)
            $offset += $pngBytes[$i].Length
        }
        foreach ($png in $pngBytes) { $writer.Write($png) }
    } finally {
        $writer.Close()
        $stream.Close()
    }
}

Write-Host "Generating $($sizes.Count) bitmaps..."
$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += ,(New-Bitmap -size $s) }

Write-Host "Writing $outPath..."
Write-Ico -Path $outPath -Bitmaps $bitmaps

foreach ($b in $bitmaps) { $b.Dispose() }

$size = (Get-Item $outPath).Length
Write-Host "Done. Icon size: $size bytes ($(($size / 1024).ToString('N1')) KB)"
