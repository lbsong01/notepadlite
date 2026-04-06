[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$iconOutputDirectory = Join-Path $repoRoot "src\NotepadLite.App\Resources"
$previewOutputDirectory = Join-Path $repoRoot "assets\icons"
$icoPath = Join-Path $iconOutputDirectory "NotepadLite.ico"
$pngPreviewPath = Join-Path $previewOutputDirectory "notepadlite-icon-preview.png"
$sizes = @(16, 24, 32, 48, 64, 128, 256)

New-Item -ItemType Directory -Path $iconOutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $previewOutputDirectory -Force | Out-Null

function New-Brush([string]$hex)
{
    return [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($hex))
}

function Fill-RoundedRectangle
{
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Brush]$Brush,
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $Graphics.FillPath($Brush, $path)
    $path.Dispose()
}

function New-IconBitmap
{
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 256.0
    $backgroundRectangle = [System.Drawing.RectangleF]::new(16 * $scale, 16 * $scale, 224 * $scale, 224 * $scale)
    $backgroundPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $corner = 52 * $scale
    $backgroundPath.AddArc($backgroundRectangle.X, $backgroundRectangle.Y, $corner, $corner, 180, 90)
    $backgroundPath.AddArc($backgroundRectangle.Right - $corner, $backgroundRectangle.Y, $corner, $corner, 270, 90)
    $backgroundPath.AddArc($backgroundRectangle.Right - $corner, $backgroundRectangle.Bottom - $corner, $corner, $corner, 0, 90)
    $backgroundPath.AddArc($backgroundRectangle.X, $backgroundRectangle.Bottom - $corner, $corner, $corner, 90, 90)
    $backgroundPath.CloseFigure()

    $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(26 * $scale, 18 * $scale),
        [System.Drawing.PointF]::new(228 * $scale, 232 * $scale),
        [System.Drawing.ColorTranslator]::FromHtml("#163252"),
        [System.Drawing.ColorTranslator]::FromHtml("#0F86E8"))
    $backgroundBlend = [System.Drawing.Drawing2D.ColorBlend]::new()
    $backgroundBlend.Colors = [System.Drawing.Color[]]@(
        [System.Drawing.ColorTranslator]::FromHtml("#163252"),
        [System.Drawing.ColorTranslator]::FromHtml("#1C5EA5"),
        [System.Drawing.ColorTranslator]::FromHtml("#0F86E8"))
    $backgroundBlend.Positions = [single[]]@(0.0, 0.56, 1.0)
    $backgroundBrush.InterpolationColors = $backgroundBlend
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $cornerAccentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(66, 105, 180, 255))
    $graphics.FillPolygon($cornerAccentBrush, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(32 * $scale, 54 * $scale),
        [System.Drawing.PointF]::new(54 * $scale, 32 * $scale),
        [System.Drawing.PointF]::new(120 * $scale, 32 * $scale),
        [System.Drawing.PointF]::new(32 * $scale, 120 * $scale)))

    $cornerShadeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(52, 10, 75, 137))
    $graphics.FillPolygon($cornerShadeBrush, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(203 * $scale, 224 * $scale),
        [System.Drawing.PointF]::new(224 * $scale, 203 * $scale),
        [System.Drawing.PointF]::new(224 * $scale, 136 * $scale),
        [System.Drawing.PointF]::new(136 * $scale, 224 * $scale)))

    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(46, 11, 26, 44))
    Fill-RoundedRectangle -Graphics $graphics -Brush $shadowBrush -X (66 * $scale) -Y (58 * $scale) -Width (136 * $scale) -Height (156 * $scale) -Radius (18 * $scale)

    $pageBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(74 * $scale, 48 * $scale),
        [System.Drawing.PointF]::new(190 * $scale, 214 * $scale),
        [System.Drawing.Color]::White,
        [System.Drawing.ColorTranslator]::FromHtml("#EAF2FB"))
    Fill-RoundedRectangle -Graphics $graphics -Brush $pageBrush -X (60 * $scale) -Y (46 * $scale) -Width (136 * $scale) -Height (162 * $scale) -Radius (18 * $scale)

    $headerBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(72 * $scale, 56 * $scale),
        [System.Drawing.PointF]::new(194 * $scale, 84 * $scale),
        [System.Drawing.ColorTranslator]::FromHtml("#F2F7FD"),
        [System.Drawing.ColorTranslator]::FromHtml("#DDEAF8"))
    $graphics.FillRectangle($headerBrush, 72 * $scale, 58 * $scale, 112 * $scale, 30 * $scale)

    $foldPoints = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(154 * $scale, 46 * $scale),
        [System.Drawing.PointF]::new(196 * $scale, 88 * $scale),
        [System.Drawing.PointF]::new(166 * $scale, 88 * $scale),
        [System.Drawing.PointF]::new(154 * $scale, 76 * $scale))
    $foldBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(160 * $scale, 50 * $scale),
        [System.Drawing.PointF]::new(202 * $scale, 88 * $scale),
        [System.Drawing.ColorTranslator]::FromHtml("#EEF5FD"),
        [System.Drawing.ColorTranslator]::FromHtml("#C6DCF5"))
    $graphics.FillPolygon($foldBrush, $foldPoints)

    $tabBrush = New-Brush "#86B9E9"
    Fill-RoundedRectangle -Graphics $graphics -Brush $tabBrush -X (82 * $scale) -Y (64 * $scale) -Width (12 * $scale) -Height (14 * $scale) -Radius (4 * $scale)
    Fill-RoundedRectangle -Graphics $graphics -Brush $tabBrush -X (104 * $scale) -Y (64 * $scale) -Width (12 * $scale) -Height (14 * $scale) -Radius (4 * $scale)
    Fill-RoundedRectangle -Graphics $graphics -Brush $tabBrush -X (126 * $scale) -Y (64 * $scale) -Width (12 * $scale) -Height (14 * $scale) -Radius (4 * $scale)

    $lineBrush = New-Brush "#D8E5F2"
    $lineBrushStrong = New-Brush "#C7D8EA"
    Fill-RoundedRectangle -Graphics $graphics -Brush $lineBrushStrong -X (82 * $scale) -Y (102 * $scale) -Width (92 * $scale) -Height (9 * $scale) -Radius (4.5 * $scale)
    Fill-RoundedRectangle -Graphics $graphics -Brush $lineBrush -X (82 * $scale) -Y (122 * $scale) -Width (92 * $scale) -Height (9 * $scale) -Radius (4.5 * $scale)
    Fill-RoundedRectangle -Graphics $graphics -Brush $lineBrush -X (82 * $scale) -Y (142 * $scale) -Width (76 * $scale) -Height (9 * $scale) -Radius (4.5 * $scale)

    $inkBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(92 * $scale, 184 * $scale),
        [System.Drawing.PointF]::new(178 * $scale, 102 * $scale),
        [System.Drawing.ColorTranslator]::FromHtml("#0E74CD"),
        [System.Drawing.ColorTranslator]::FromHtml("#0A58A9"))
    $inkPen = [System.Drawing.Pen]::new($inkBrush, [Math]::Max(4, 17 * $scale))
    $inkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $inkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $inkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawCurve($inkPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(86 * $scale, 182 * $scale),
        [System.Drawing.PointF]::new(101 * $scale, 176 * $scale),
        [System.Drawing.PointF]::new(112 * $scale, 165 * $scale),
        [System.Drawing.PointF]::new(122 * $scale, 150 * $scale),
        [System.Drawing.PointF]::new(148 * $scale, 124 * $scale),
        [System.Drawing.PointF]::new(174 * $scale, 100 * $scale)),
        0.45)

    $nibPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml("#153A63"), [Math]::Max(3, 10 * $scale))
    $nibPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $nibPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($nibPen, 160 * $scale, 100 * $scale, 178 * $scale, 100 * $scale)
    $graphics.DrawLine($nibPen, 178 * $scale, 100 * $scale, 170 * $scale, 118 * $scale)

    $backgroundPath.Dispose()
    $backgroundBrush.Dispose()
    $cornerAccentBrush.Dispose()
    $cornerShadeBrush.Dispose()
    $shadowBrush.Dispose()
    $pageBrush.Dispose()
    $headerBrush.Dispose()
    $foldBrush.Dispose()
    $tabBrush.Dispose()
    $lineBrush.Dispose()
    $lineBrushStrong.Dispose()
    $inkBrush.Dispose()
    $inkPen.Dispose()
    $nibPen.Dispose()
    $graphics.Dispose()

    return $bitmap
}

$pngEntries = [System.Collections.Generic.List[object]]::new()

foreach ($size in $sizes)
{
    $bitmap = New-IconBitmap -Size $size
    $memoryStream = [System.IO.MemoryStream]::new()
    $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $memoryStream.ToArray()
    $pngEntries.Add([PSCustomObject]@{
        Size = $size
        Bytes = $pngBytes
    })

    if ($size -eq 256)
    {
        [System.IO.File]::WriteAllBytes($pngPreviewPath, $pngBytes)
    }

    $memoryStream.Dispose()
    $bitmap.Dispose()
}

$fileStream = [System.IO.File]::Create($icoPath)
$writer = [System.IO.BinaryWriter]::new($fileStream)

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$pngEntries.Count)

$offset = 6 + (16 * $pngEntries.Count)

foreach ($entry in $pngEntries)
{
    $sizeByte = if ($entry.Size -ge 256) { 0 } else { [byte]$entry.Size }
    $writer.Write([byte]$sizeByte)
    $writer.Write([byte]$sizeByte)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$entry.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $entry.Bytes.Length
}

foreach ($entry in $pngEntries)
{
    $writer.Write($entry.Bytes)
}

$writer.Dispose()
$fileStream.Dispose()

Write-Host "Generated icon at $icoPath"
Write-Host "Generated preview at $pngPreviewPath"