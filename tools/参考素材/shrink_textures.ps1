# 把参考素材的贴图压到「田里一株小草」该有的尺寸。
#
# 【为什么要压】源工程给的贴图是 **4096 x 4096**（一张 1.3 MB），
# 15 张就是 12.7 MB —— 对一株在田里、在第三人称视角下只有几十像素的植物纯属浪费，
# 而且会永远躺在 git 历史里。压到 256² 后每张约 18 KB（**约 1/70**）。
#
# 为什么用 PowerShell + System.Drawing 而不是 Python：
#   本机没装 Pillow，而 Python 标准库不能编解码 JPEG；
#   Windows 自带的 GDI+ 编解码器可以直接用（需要 FullLanguage 模式）。
#
# 用法（在仓库根目录）：
#     powershell -NoProfile -ExecutionPolicy Bypass -File tools\参考素材\shrink_textures.ps1

param(
    [int]$CropSize = 256,       # 作物贴图边长
    [int]$SoilSize = 512,       # 地面（土质）贴图边长
    [int]$Quality  = 82
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Join-Path $PSScriptRoot '..\..\cultivation\Assets\Art\灵田'
$root = (Resolve-Path $root).Path

$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
         Where-Object { $_.MimeType -eq 'image/jpeg' }

function Save-Jpeg($bmp, $path, $quality) {
    $ps = New-Object System.Drawing.Imaging.EncoderParameters 1
    $ps.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
        [System.Drawing.Imaging.Encoder]::Quality, [int]$quality)
    $bmp.Save($path, $codec, $ps)
    $ps.Dispose()
}

function Shrink-One($file, $size) {
    $before = (Get-Item $file).Length
    # 先整张读进内存再释放句柄，否则覆盖保存会失败（文件被占）
    $img = [System.Drawing.Image]::FromFile($file)
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($img, 0, 0, $size, $size)
    $g.Dispose()
    $img.Dispose()
    Save-Jpeg $bmp $file $Quality
    $bmp.Dispose()
    $after = (Get-Item $file).Length
    Write-Host ("  {0,-52} {1,7:N0} KB -> {2,6:N0} KB" -f `
        $file.Replace($root + '\', ''), ($before / 1KB), ($after / 1KB))
    return @($before, $after)
}

Write-Host "=== 作物贴图 -> ${CropSize}x${CropSize} ==="
$b = 0; $a = 0
Get-ChildItem -Path $root -Recurse -File -Filter '*_Color.jpg' | ForEach-Object {
    $r = Shrink-One $_.FullName $CropSize
    $b += $r[0]; $a += $r[1]
}
Write-Host ("作物贴图合计 {0:N1} MB -> {1:N2} MB" -f ($b / 1MB), ($a / 1MB))

Write-Host "=== 土质贴图 -> ${SoilSize}x${SoilSize}（png 转 jpg） ==="
Get-ChildItem -Path $root -Recurse -File | Where-Object { $_.Name -eq 'loam.png' } | ForEach-Object {
    $img = [System.Drawing.Image]::FromFile($_.FullName)
    $bmp = New-Object System.Drawing.Bitmap $SoilSize, $SoilSize
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($img, 0, 0, $SoilSize, $SoilSize)
    $g.Dispose(); $img.Dispose()
    $out = Join-Path $_.DirectoryName 'loam_512.jpg'
    Save-Jpeg $bmp $out 88
    $bmp.Dispose()
    Write-Host ("  {0,-52} {1,7:N0} KB -> {2,6:N0} KB" -f $_.Name, ($_.Length / 1KB), ((Get-Item $out).Length / 1KB))
    Remove-Item $_.FullName          # 4K 的 png 换成 512 的 jpg，删掉原图
    # 顺手指掉导入器可能留下的 png 元数据
    $meta = $_.FullName + '.meta'
    if (Test-Path $meta) { Remove-Item $meta }
}

Write-Host "=== 完成 ==="
$all = Get-ChildItem -Path $root -Recurse -File
Write-Host ("{0} 个文件，合计 {1:N2} MB" -f $all.Count, (($all | Measure-Object Length -Sum).Sum / 1MB))
