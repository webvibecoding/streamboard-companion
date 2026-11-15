param([string]$PreviewPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetDirectory = Join-Path $PSScriptRoot '../src/Streamboard.Companion.Windows/Assets'
[xml]$art = Get-Content -LiteralPath (Join-Path $assetDirectory 'Streamboard.svg') -Raw
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-IconBitmap([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size / 16.0, $size / 16.0)
        foreach ($shape in $art.DocumentElement.ChildNodes) {
            switch ($shape.LocalName) {
                'rect' {
                    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($shape.fill))
                    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
                    try {
                        $x = [float]$shape.x
                        $y = [float]$shape.y
                        $width = [float]$shape.width
                        $height = [float]$shape.height
                        $d = [float]$shape.rx * 2
                        $path.AddArc($x, $y, $d, $d, 180, 90)
                        $path.AddArc($x + $width - $d, $y, $d, $d, 270, 90)
                        $path.AddArc($x + $width - $d, $y + $height - $d, $d, $d, 0, 90)
                        $path.AddArc($x, $y + $height - $d, $d, $d, 90, 90)
                        $path.CloseFigure()
                        $graphics.FillPath($brush, $path)
                    }
                    finally { $path.Dispose(); $brush.Dispose() }
                }
                'polyline' {
                    $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml($shape.stroke), [float]$shape.'stroke-width')
                    try {
                        $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                        [System.Drawing.PointF[]]$points = foreach ($point in ($shape.points -split ' ')) {
                            $x, $y = $point -split ','
                            [System.Drawing.PointF]::new([float]$x, [float]$y)
                        }
                        $graphics.DrawLines($pen, $points)
                    }
                    finally { $pen.Dispose() }
                }
                default { throw "Unsupported SVG shape: $($shape.LocalName)" }
            }
        }
    }
    finally { $graphics.Dispose() }
    return $bitmap
}

function New-IconFrame([System.Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        if ($size -eq 256) {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        else {
            $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
            $writer.Write([int]40)
            $writer.Write([int]$size)
            $writer.Write([int]($size * 2))
            $writer.Write([int16]1)
            $writer.Write([int16]32)
            foreach ($unused in 1..6) { $writer.Write([int]0) }
            $mask = [byte[]]::new($maskStride * $size)
            for ($y = $size - 1; $y -ge 0; $y--) {
                for ($x = 0; $x -lt $size; $x++) {
                    $pixel = $bitmap.GetPixel($x, $y)
                    $writer.Write($pixel.ToArgb())
                    if ($pixel.A -eq 0) {
                        $index = ($size - 1 - $y) * $maskStride + ($x -shr 3)
                        $mask[$index] = $mask[$index] -bor (128 -shr ($x -band 7))
                    }
                }
            }
            $writer.Write($mask)
        }
        return ,$stream.ToArray()
    }
    finally { $writer.Dispose() }
}

$frames = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    try {
        if ($size -eq 128 -and $PreviewPath) {
            $bitmap.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        ,[byte[]](New-IconFrame $bitmap)
    }
    finally { $bitmap.Dispose() }
}

$icon = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assetDirectory 'Streamboard.ico')))
try {
    $icon.Write([uint16]0)
    $icon.Write([uint16]1)
    $icon.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = [byte]($sizes[$i] % 256)
        $icon.Write($dimension)
        $icon.Write($dimension)
        $icon.Write([uint16]0)
        $icon.Write([uint16]1)
        $icon.Write([uint16]32)
        $icon.Write([int]$frames[$i].Length)
        $icon.Write([int]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $icon.Write([byte[]]$frame) }
}
finally { $icon.Dispose() }
