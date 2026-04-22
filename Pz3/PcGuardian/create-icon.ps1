$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$assetsDir = Join-Path $projectRoot "Assets"
$icoPath = Join-Path $assetsDir "PcGuardian.ico"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$size = 256
$bitmap = New-Object System.Drawing.Bitmap $size, $size
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddLines([System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(128, 18),
    [System.Drawing.PointF]::new(220, 52),
    [System.Drawing.PointF]::new(210, 150),
    [System.Drawing.PointF]::new(128, 235),
    [System.Drawing.PointF]::new(46, 150),
    [System.Drawing.PointF]::new(36, 52)
))
$path.CloseFigure()

$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Rectangle]::new(30, 18, 196, 220),
    [System.Drawing.Color]::FromArgb(22, 132, 255),
    [System.Drawing.Color]::FromArgb(16, 190, 117),
    [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
)
$graphics.FillPath($brush, $path)
$graphics.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 10), $path)
$graphics.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)), 112, 72, 32, 104)
$graphics.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)), 76, 108, 104, 32)

$pngStream = New-Object System.IO.MemoryStream
$bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngStream.ToArray()

$fileStream = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter $fileStream
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$pngBytes.Length)
$writer.Write([UInt32]22)
$writer.Write($pngBytes)
$writer.Dispose()
$fileStream.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Icon created: $icoPath"
