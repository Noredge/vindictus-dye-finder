# Original vector mark: a diagonal artist's paintbrush, rendered at several icon sizes.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/DyeFinder.App/Assets'
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
$frames=@()
foreach($size in @(16,32,48,64,256)) {
    $bitmap=[System.Drawing.Bitmap]::new($size,$size)
    $g=[System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size/256.0,$size/256.0)
    $g.Clear([System.Drawing.Color]::FromArgb(13,21,32))
    $g.TranslateTransform(128,128);$g.RotateTransform(45);$g.TranslateTransform(-128,-128)
    $handle=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $handle.AddBezier(116,43,114,23,142,23,140,43)
    $handle.AddLine(140,43,149,137);$handle.AddLine(149,137,107,137);$handle.CloseFigure()
    $fill=[System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Point]::new(107,30),[System.Drawing.Point]::new(149,137),[System.Drawing.Color]::FromArgb(105,218,239),[System.Drawing.Color]::FromArgb(54,116,224))
    $g.FillPath($fill,$handle)
    $metal=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(201,219,229))
    $g.FillRectangle($metal,107,140,42,22)
    $bristles=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $bristles.AddLine(107,166,149,166)
    $bristles.AddBezier(149,166,161,193,137,211,101,220)
    $bristles.AddBezier(101,220,117,198,97,190,107,166)
    $bristles.CloseFigure()
    $hair=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(247,225,184))
    $g.FillPath($hair,$bristles)
    $pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(192,161,118),4)
    $g.DrawBezier($pen,128,174,137,189,127,201,115,207)
    $stream=[System.IO.MemoryStream]::new();$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $frames+=,@{Size=$size;Bytes=$stream.ToArray()}
    if($size -eq 256){[System.IO.File]::WriteAllBytes((Join-Path $assetRoot 'DyeFinder.png'),$stream.ToArray())}
    $stream.Dispose();$pen.Dispose();$hair.Dispose();$bristles.Dispose();$metal.Dispose();$fill.Dispose();$handle.Dispose();$g.Dispose();$bitmap.Dispose()
}
$iconStream=[System.IO.File]::Create((Join-Path $assetRoot 'DyeFinder.ico'))
$writer=[System.IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
    $offset=6+16*$frames.Count
    foreach($frame in $frames){$encodedSize=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$encodedSize);$writer.Write([byte]$encodedSize);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose();$iconStream.Dispose()}
