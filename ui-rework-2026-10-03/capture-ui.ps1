param(
    [ValidateSet('前','后')] [string]$Phase = '后',
    [ValidateSet('1920x1080','1280x720')] [string]$Resolution = '1920x1080',
    [ValidateSet('角色','设施','宗门','塔','标题')] [string]$Group = '角色',
    [switch]$Prepare
)
# 从项目根执行；只读场景并在 Play 中打开真实面板，不保存场景或存档。
. ([scriptblock]::Create((Get-Content '.dsh/uni.ps1' -Raw)))
$captureRoot = Join-Path $PWD 'cultivation/screenshots'
New-Item -ItemType Directory -Path $captureRoot -Force | Out-Null
$scene = switch ($Group) {
    '角色' {'3C_Testbed'} '设施' {'village'} '宗门' {'Sect'} '塔' {'Demon-Suppressing Tower'} '标题' {'StartScene'}
}
if ($Prepare) {
    Uni 'play:off' 30
    Start-Sleep -Seconds 3
    $mode = if ($Phase -eq '前') {'before'} else {'after'}
    Uni @("inkqa:$mode", "screen:$Resolution", "open:Assets/Scenes/$scene.scene") 30
    Uni 'play:on' 30
    Start-Sleep -Seconds 3
}
if ($Group -ne '标题') { Uni 'inkqa:seed' 30 }
$panels = switch ($Group) {
    '角色' { @{'背包'='role:0';'境界'='role:1';'神通'='role:2';'法宝'='role:3';'灵阵'='role:4';'战阵'='role:5';'坐骑'='role:6';'外观'='role:7';'闭关修炼'='cultivation:0';'境界突破'='cultivation:1';'转修功法'='cultivation:2';'丹房'='alchemy';'药材选择'='materials';'灵田总览'='field';'功德堂'='contribution';'暂停'='pause';'身陨'='death';'HUD'='hud';'Toast'='toast';'起名'='name'} }
    '设施' { @{'灵田地块'='plot:0';'灵田生长中'='plot:1';'灵田成熟'='plot:2';'对话'='dialogue';'传送'='teleport'} }
    '宗门' { @{'对话宗门'='dialogue';'传送宗门'='teleport'} }
    '塔' { @{'镇妖塔死亡选择'='tower';'镇妖塔HUD'='hud'} }
    '标题' { @{'主菜单'='menu';'存档选择'='saves'} }
}
foreach ($label in ($panels.Keys | Sort-Object)) {
    $result = Uni ('inkqa:' + $panels[$label]) 30
    Write-Output "$label : $result"
    if ($result -match 'FAIL|异常|TIMEOUT') { continue }
    Start-Sleep -Milliseconds 1300
    Uni "shot2:$captureRoot/UI_${label}_${Phase}_${Resolution}.png" 30
    Start-Sleep -Milliseconds 200
}
Uni 'console:get:12' 30
Uni 'console:errors' 30
