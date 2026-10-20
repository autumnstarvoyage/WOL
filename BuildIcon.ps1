Add-Type -AssemblyName System.Drawing

$iconDir = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force -Path $iconDir | Out-Null

function New-RoundedPath([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

# Draw once at high resolution; separate PNG frames keep the icon crisp in Explorer and the taskbar.
$bitmap = [System.Drawing.Bitmap]::new(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.Clear([System.Drawing.Color]::Transparent)

$bounds = [System.Drawing.Rectangle]::new(10, 10, 492, 492)
$tile = New-RoundedPath 10 10 492 492 112
$background = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bounds, [System.Drawing.Color]::FromArgb(255, 25, 59, 180), [System.Drawing.Color]::FromArgb(255, 120, 54, 220), [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$graphics.FillPath($background, $tile)

# Soft luminous color blooms behind the glass device.
$cyanGlow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(62, 38, 238, 255))
$pinkGlow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(60, 255, 89, 195))
$graphics.FillEllipse($cyanGlow, 4, 250, 250, 250)
$graphics.FillEllipse($pinkGlow, 300, 0, 230, 230)

# Wi-Fi / wake signal: three bright arcs that lead into the device.
$signalPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(235, 220, 250, 255), 15)
$signalPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$signalPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($signalPen, 154, 55, 204, 178, 218, 104)
$signalPen.Width = 12
$graphics.DrawArc($signalPen, 184, 84, 144, 132, 218, 104)
$signalPen.Width = 10
$graphics.DrawArc($signalPen, 216, 113, 80, 82, 218, 104)

# Translucent glass screen, with a cool-tinted inner surface and bright rim.
$screen = New-RoundedPath 70 178 372 242 55
$glassFill = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(70, 178, 372, 242), [System.Drawing.Color]::FromArgb(196, 230, 252, 255), [System.Drawing.Color]::FromArgb(148, 168, 219, 255), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$glassRim = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(220, 245, 252, 255), 8)
$graphics.FillPath($glassFill, $screen)
$graphics.DrawPath($glassRim, $screen)

# A subtle tinted screen inset gives the glass depth.
$inset = New-RoundedPath 91 199 330 193 38
$insetFill = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(91, 199, 330, 193), [System.Drawing.Color]::FromArgb(208, 25, 67, 145), [System.Drawing.Color]::FromArgb(224, 39, 45, 116), [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$graphics.FillPath($insetFill, $inset)

# Glass reflection across the upper edge.
$shine = New-Object System.Drawing.Drawing2D.GraphicsPath
$shine.AddArc(88, 195, 330, 92, 180, 80)
$shinePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(94, 255, 255, 255), 7)
$shinePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$shinePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawPath($shinePen, $shine)

# Power symbol: a luminous cyan ring and stem, readable even at small sizes.
$powerGlow = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(100, 46, 245, 255), 30)
$powerPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 128, 250, 255), 20)
$powerGlow.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$powerGlow.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$powerPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$powerPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($powerGlow, 195, 239, 122, 122, 42, 276)
$graphics.DrawLine($powerGlow, 256, 223, 256, 279)
$graphics.DrawArc($powerPen, 195, 239, 122, 122, 42, 276)
$graphics.DrawLine($powerPen, 256, 223, 256, 279)

# Small warm packet spark makes the network wake-up action feel immediate.
$sparkBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 192, 91))
$spark = @(
    [System.Drawing.Point]::new(367, 222), [System.Drawing.Point]::new(351, 258),
    [System.Drawing.Point]::new(371, 253), [System.Drawing.Point]::new(359, 282),
    [System.Drawing.Point]::new(390, 243), [System.Drawing.Point]::new(370, 247)
)
$graphics.FillPolygon($sparkBrush, $spark)

# Multi-size ICO with PNG-compressed frames.
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $frame = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $frameGraphics = [System.Drawing.Graphics]::FromImage($frame)
    $frameGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $frameGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $frameGraphics.DrawImage($bitmap, 0, 0, $size, $size)
    $stream = New-Object System.IO.MemoryStream
    $frame.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose(); $frameGraphics.Dispose(); $frame.Dispose()
}

$output = Join-Path $iconDir 'lumawake.ico'
$file = [System.IO.File]::Create($output)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
    $writer.Write($dimension); $writer.Write($dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$frame.Bytes.Length); $writer.Write([UInt32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write($frame.Bytes) }
$writer.Flush(); $writer.Dispose(); $file.Dispose()

$graphics.Dispose(); $bitmap.Dispose(); $background.Dispose(); $tile.Dispose(); $cyanGlow.Dispose(); $pinkGlow.Dispose()
$signalPen.Dispose(); $screen.Dispose(); $glassFill.Dispose(); $glassRim.Dispose(); $inset.Dispose(); $insetFill.Dispose()
$shine.Dispose(); $shinePen.Dispose(); $powerGlow.Dispose(); $powerPen.Dispose(); $sparkBrush.Dispose()
Write-Output $output
