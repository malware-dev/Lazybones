# Generates Lazybones/Assets/appicon.png (1024×1024 master) and the
# multi-resolution appicon.ico used by Windows. The .ico entries are
# downsampled from the master so the design is identical at every size —
# no separate small-variant artwork.
#
# Run from the repo root: pwsh tools/generate-icon.ps1
#
# The design replicates the in-app ProgressRings clock face:
#   - Dark charcoal disk
#   - Outer thin ring (cycle-goal blue, ~85% fill, rounded caps)
#   - Inner thick ring (standing-mode green, ~70% fill, rounded caps)
#   - Light gray upward chevron centered, slightly above midline,
#     reinforcing the "stand up" message

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$outPng = Join-Path $repoRoot 'Lazybones/Assets/appicon.png'
$outIco = Join-Path $repoRoot 'Lazybones/Assets/appicon.ico'

# Palette — matches Lazybones/Features/Stats/ProgressRings.cs
$diskColor = [System.Drawing.Color]::FromArgb(255, 0x1A, 0x1A, 0x1A)
$trackColor = [System.Drawing.Color]::FromArgb(40, 255, 255, 255)
$outerColor = [System.Drawing.Color]::FromArgb(255, 0x4C, 0xC2, 0xFF)
$innerColor = [System.Drawing.Color]::FromArgb(255, 0x00, 0xD4, 0x6A)
$chevronColor = [System.Drawing.Color]::FromArgb(255, 0xB0, 0xB0, 0xB0)

function Render-FullIcon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Geometry — designed at 1024, scaled to $size.
    $s = $size / 1024.0
    $margin = [int](32 * $s)

    # Disk
    $diskBrush = New-Object System.Drawing.SolidBrush($diskColor)
    $g.FillEllipse($diskBrush, [int]$margin, [int]$margin, [int]($size - 2 * $margin), [int]($size - 2 * $margin))
    $diskBrush.Dispose()

    $center = $size / 2.0

    # Outer ring (cycle-goal blue, ~85% fill)
    $outerCenterR = 430 * $s
    $outerT = [Math]::Max(1.0, 50 * $s)
    $outerRect = New-Object System.Drawing.RectangleF(($center - $outerCenterR), ($center - $outerCenterR), (2 * $outerCenterR), (2 * $outerCenterR))

    $trackPen = New-Object System.Drawing.Pen($trackColor, $outerT)
    $g.DrawEllipse($trackPen, $outerRect)
    $trackPen.Dispose()

    $outerPen = New-Object System.Drawing.Pen($outerColor, $outerT)
    $outerPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $outerPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($outerPen, $outerRect, -90.0, 360.0 * 0.85)
    $outerPen.Dispose()

    # Inner ring (standing-mode green, ~70% fill)
    $innerCenterR = 345 * $s
    $innerT = [Math]::Max(2.0, 80 * $s)
    $innerRect = New-Object System.Drawing.RectangleF(($center - $innerCenterR), ($center - $innerCenterR), (2 * $innerCenterR), (2 * $innerCenterR))

    $trackPen = New-Object System.Drawing.Pen($trackColor, $innerT)
    $g.DrawEllipse($trackPen, $innerRect)
    $trackPen.Dispose()

    $innerPen = New-Object System.Drawing.Pen($innerColor, $innerT)
    $innerPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $innerPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($innerPen, $innerRect, -90.0, 360.0 * 0.70)
    $innerPen.Dispose()

    # Upward chevron — two strokes meeting at the apex, rounded caps to match
    # the rings. Sits slightly above the disk midline so it reads as "rising"
    # rather than centred-and-static.
    $chevronPen = New-Object System.Drawing.Pen($chevronColor, ($size * 0.075))
    $chevronPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $chevronPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $chevronPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $chevronCy = $center - ($size * 0.046875)
    $halfW = $size * 0.16
    $halfH = $size * 0.11

    $p1 = New-Object System.Drawing.PointF([single]($center - $halfW), [single]($chevronCy + $halfH))
    $p2 = New-Object System.Drawing.PointF([single]$center, [single]($chevronCy - $halfH))
    $p3 = New-Object System.Drawing.PointF([single]($center + $halfW), [single]($chevronCy + $halfH))
    [System.Drawing.PointF[]]$pointsArr = @($p1, $p2, $p3)
    $g.DrawLines($chevronPen, $pointsArr)
    $chevronPen.Dispose()

    $g.Dispose()
    return $bmp
}

# Master 1024 — saved to disk and used as the source for every .ico entry.
$master = Render-FullIcon 1024
$master.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote $outPng"

# Multi-resolution .ico. Each entry is the master downsampled with
# high-quality bicubic resampling, so the design is identical at every size.
# Modern Windows (Vista+) reads PNG-encoded ico entries natively.
$icoSizes = @(16, 24, 32, 48, 64, 128, 256)

$pngBytesPerEntry = @()
foreach ($size in $icoSizes) {
    $resized = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rg = [System.Drawing.Graphics]::FromImage($resized)
    $rg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $rg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $rg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $rg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $rg.DrawImage($master, 0, 0, $size, $size)
    $rg.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()
    $pngBytesPerEntry += , $ms.ToArray()
}
$sizesAndVariants = $icoSizes | ForEach-Object { @{Size = $_} }

# Write the .ico binary.
$file = [System.IO.File]::Create($outIco)
$writer = New-Object System.IO.BinaryWriter($file)

# ICONDIR header (6 bytes)
$writer.Write([uint16]0)                               # reserved
$writer.Write([uint16]1)                               # type = 1 (icon)
$writer.Write([uint16]$sizesAndVariants.Count)         # entry count

# Compute offset to start of image data
$headerSize = 6
$entrySize = 16
$dataStart = $headerSize + $entrySize * $sizesAndVariants.Count

$runningOffset = $dataStart
for ($i = 0; $i -lt $sizesAndVariants.Count; $i++) {
    $size = $sizesAndVariants[$i].Size
    $bytes = $pngBytesPerEntry[$i]

    # ICONDIRENTRY (16 bytes)
    # 0 in width/height means 256
    $w = if ($size -ge 256) { 0 } else { $size }
    $h = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)                             # colorCount (0 = no palette)
    $writer.Write([byte]0)                             # reserved
    $writer.Write([uint16]1)                           # planes
    $writer.Write([uint16]32)                          # bitCount (32bpp RGBA)
    $writer.Write([uint32]$bytes.Length)               # bytesInRes
    $writer.Write([uint32]$runningOffset)              # imageOffset

    $runningOffset += $bytes.Length
}

# Image data — concatenated PNG payloads in the same order as the entries
foreach ($bytes in $pngBytesPerEntry) {
    $writer.Write($bytes)
}

$writer.Dispose()
$file.Dispose()
Write-Host "Wrote $outIco ($($sizesAndVariants.Count) entries)"

$master.Dispose()
