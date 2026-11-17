$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path (Split-Path $PSScriptRoot) 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
function Draw-Mark($graphics, $x, $y, $size) {
  $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#7563DF'))
  $graphics.FillRectangle($brush, $x, $y, $size, $size)
  $pen = [Drawing.Pen]::new([Drawing.Color]::White, $size / 22)
  $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
  $graphics.DrawLine($pen, $x+$size*.19, $y+$size*.3, $x+$size*.64, $y+$size*.3)
  $graphics.DrawLine($pen, $x+$size*.4, $y+$size*.18, $x+$size*.4, $y+$size*.3)
  $graphics.DrawBezier($pen, $x+$size*.55, $y+$size*.3, $x+$size*.5, $y+$size*.55, $x+$size*.35, $y+$size*.65, $x+$size*.2, $y+$size*.7)
  $graphics.DrawBezier($pen, $x+$size*.3, $y+$size*.3, $x+$size*.33, $y+$size*.53, $x+$size*.45, $y+$size*.61, $x+$size*.58, $y+$size*.65)
  $graphics.DrawLines($pen, [Drawing.PointF[]]@([Drawing.PointF]::new($x+$size*.57,$y+$size*.81),[Drawing.PointF]::new($x+$size*.73,$y+$size*.43),[Drawing.PointF]::new($x+$size*.88,$y+$size*.81)))
  $graphics.DrawLine($pen,$x+$size*.63,$y+$size*.69,$x+$size*.82,$y+$size*.69)
  $pen.Dispose(); $brush.Dispose()
}
$sizes = @(16,24,32,48,64,128,256)
$images = @()
foreach ($size in $sizes) {
  $bitmap = [Drawing.Bitmap]::new($size,$size); $g = [Drawing.Graphics]::FromImage($bitmap); $g.SmoothingMode = 'AntiAlias'; Draw-Mark $g 0 0 $size
  $ms = [IO.MemoryStream]::new(); $bitmap.Save($ms,[Drawing.Imaging.ImageFormat]::Png); $images += ,$ms.ToArray(); $g.Dispose();$bitmap.Dispose();$ms.Dispose()
}
$fs = [IO.File]::Create((Join-Path $assetDir 'yike.ico')); $writer = [IO.BinaryWriter]::new($fs)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count);$offset=6+16*$images.Count
for ($i=0;$i -lt $images.Count;$i++) { $d=$sizes[$i]%256;$writer.Write([byte]$d);$writer.Write([byte]$d);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset);$offset+=$images[$i].Length }
foreach($im in $images){$writer.Write([byte[]]$im)};$writer.Dispose()
foreach ($scale in @(1,2)) {
 $b=[Drawing.Bitmap]::new(164*$scale,314*$scale);$g=[Drawing.Graphics]::FromImage($b);$g.ScaleTransform($scale,$scale);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#252138'))
 $pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#493D70'),1)
 for($i=-80;$i -lt 340;$i+=28){$g.DrawEllipse($pen,-130,$i,300,300)}
 Draw-Mark $g 24 38 56
 $white=[Drawing.SolidBrush]::new([Drawing.Color]::White);$muted=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#D2CAE8'))
 $f=[Drawing.Font]::new('Microsoft YaHei UI',24,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel);$g.DrawString('译刻',$f,$white,20,113)
 $f2=[Drawing.Font]::new('Microsoft YaHei UI',12,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
 $g.DrawString("划词即译`n让理解，自然发生。",$f2,$muted,22,162)
 $f3=[Drawing.Font]::new('Segoe UI',9,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel);$g.DrawString('YIKE / WINDOWS',$f3,$muted,24,279)
 $b.Save((Join-Path $assetDir "wizard-$scale.bmp"),[Drawing.Imaging.ImageFormat]::Bmp)
 $g.Dispose();$b.Dispose();$pen.Dispose();$white.Dispose();$muted.Dispose();$f.Dispose();$f2.Dispose();$f3.Dispose()
}
Write-Host 'Brand assets generated.'
