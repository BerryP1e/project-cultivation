# 规格化导出：等比缩放进透明画布，不抠色、不补画、不裁掉主体。
param([string]$Map = '')
. (Join-Path $PSScriptRoot '../paths.ps1')
$inkPaths = Get-InkUIPaths $PSScriptRoot
$assetDir = $inkPaths.Authoring
if (-not $Map) { $Map = Join-Path $assetDir 'generation-map.json' }
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class AbilityAlphaStats {
    public static int[] Count(Bitmap image) {
        var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            byte[] row=new byte[image.Width*4]; int transparent=0, partial=0, opaque=0;
            for(int y=0;y<image.Height;y++) {
                Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),row,0,row.Length);
                for(int x=3;x<row.Length;x+=4) { if(row[x]==0) transparent++; else if(row[x]==255) opaque++; else partial++; }
            }
            return new[]{transparent,partial,opaque};
        } finally { image.UnlockBits(bits); }
    }
}
'@ -ReferencedAssemblies ([Drawing.Bitmap].Assembly.Location),([Drawing.Rectangle].Assembly.Location)
$generation = Get-Content -LiteralPath $Map -Raw -Encoding utf8 | ConvertFrom-Json
$engineDir = Join-Path $inkPaths.Runtime 'AbilityArt'
New-Item -ItemType Directory -Force -Path $engineDir,(Join-Path $assetDir 'source'),(Join-Path $assetDir 'export') | Out-Null
$manifest = foreach ($asset in $generation) {
    $file = "$($asset.Kind)-ability-$($asset.Id).png"
    $sourcePath = Join-Path $assetDir "source/$file"
    $inputPath = if ([IO.Path]::IsPathRooted($asset.Source)) { $asset.Source } else { Join-Path $assetDir $asset.Source }
    if ([IO.Path]::GetFullPath($inputPath) -ne [IO.Path]::GetFullPath($sourcePath)) { Copy-Item -LiteralPath $inputPath -Destination $sourcePath }
    $source = [Drawing.Bitmap]::new($sourcePath)
    $width = if ($asset.Kind -eq 'icon') { 256 } else { 1024 }
    $height = if ($asset.Kind -eq 'icon') { 256 } else { 576 }
    $target = [Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $draw = [Drawing.Graphics]::FromImage($target)
    try {
        $draw.Clear([Drawing.Color]::Transparent)
        $draw.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $draw.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $draw.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $scale = [Math]::Min($width*.94/$source.Width,$height*.94/$source.Height)
        $drawWidth = [int][Math]::Round($source.Width*$scale)
        $drawHeight = [int][Math]::Round($source.Height*$scale)
        $draw.DrawImage($source,[Drawing.Rectangle]::new([int](($width-$drawWidth)/2),[int](($height-$drawHeight)/2),$drawWidth,$drawHeight))
        $stats = [AbilityAlphaStats]::Count($target)
        if ($stats[0] -eq 0 -or $stats[1]+$stats[2] -eq 0) { throw "Not a visible transparent asset: $file" }
        $export = Join-Path $assetDir "export/$file"
        $target.Save($export,[Drawing.Imaging.ImageFormat]::Png)
        Copy-Item -LiteralPath $export -Destination (Join-Path $engineDir $file)
        [pscustomobject]@{ Id=$asset.Id; Kind=$asset.Kind; File=$file; Width=$width; Height=$height; SourceWidth=$source.Width; SourceHeight=$source.Height; TransparentPixels=$stats[0]; PartialAlphaPixels=$stats[1]; OpaquePixels=$stats[2]; TransparentFraction=[Math]::Round($stats[0]/($width*$height),4); EnginePath="Assets/resources/UI/InkUI/AbilityArt/$file" }
    } finally { $draw.Dispose(); $source.Dispose(); $target.Dispose() }
}
$manifestJson = ($manifest | ConvertTo-Json -Depth 5).Replace("`r`n", "`n") + "`n"
[IO.File]::WriteAllText((Join-Path $assetDir 'manifest.json'),$manifestJson,[Text.UTF8Encoding]::new($false))
Write-Output "Exported $($manifest.Count) RGBA assets; sources retained; no scene/data writes"
