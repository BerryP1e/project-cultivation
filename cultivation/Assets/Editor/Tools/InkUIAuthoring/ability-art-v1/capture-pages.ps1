# 从仓库根运行；只临时授予现有神通，不保存场景/存档。
param([string[]]$Phases = @('前','后'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PWD '.dsh/uni.ps1')
function CaptureAbilityPage([string]$command,[string]$label,[string]$phase,[string]$resolution) {
    $result = Uni "inkqa:$command" 30
    if ($result -notmatch '^OK ') { throw $result }
    Uni "screen:$resolution" 30 | Out-Null
    Start-Sleep -Milliseconds 1000
    $path = Join-Path $PWD "cultivation/screenshots/UI_${label}_插画接入${phase}_${resolution}.png"
    Uni "shot2:$path" 30 | Out-Null
    Start-Sleep -Milliseconds 400
    $bytes = [IO.File]::ReadAllBytes($path)
    $width = $bytes[16]*16777216+$bytes[17]*65536+$bytes[18]*256+$bytes[19]
    $height = $bytes[20]*16777216+$bytes[21]*65536+$bytes[22]*256+$bytes[23]
    if ("${width}x${height}" -ne $resolution) { throw "Wrong actual screenshot dimensions: $path" }
    Write-Output "PASS capture $label $phase $resolution"
}
try {
    foreach ($phase in $Phases) {
        $mode = if ($phase -eq '前') { 'artbefore' } else { 'artafter' }
        Uni "inkqa:$mode" 30
        Uni 'play:on' 30
        Start-Sleep -Seconds 3
        Uni 'inkqa:seed' 30
        foreach ($resolution in @('1920x1080','1280x720')) {
            foreach ($page in @(@('role:2','神通'),@('role:3','法宝'),@('role:4','灵阵'),@('hud','HUD'))) {
                CaptureAbilityPage $page[0] $page[1] $phase $resolution
            }
            if ($phase -eq '后') {
                Uni 'inkqa:artcheck' 30 | Tee-Object -FilePath "cultivation/screenshots/InkUI-ability-artchecks-$resolution.txt"
                Uni 'inkqa:validate' 30 | Tee-Object -FilePath "cultivation/screenshots/InkUI-ability-gameplaychecks-$resolution.txt"
                foreach ($abilityId in @('bingbao_shu','fentian_yanshu','leidong_qianshan','shunlei_tianshan','hanxu','pingxu_yufeng','qianjie_leiyu','leiyun')) {
                    CaptureAbilityPage "ability:ability_$abilityId" "神通-$abilityId" $phase $resolution
                }
            }
        }
        Uni 'console:errors' 30
        Uni 'play:off' 30
        Start-Sleep -Seconds 1
    }
} finally {
    Uni 'inkqa:artafter' 30 | Out-Null
    Uni 'play:off' 30 | Out-Null
}
