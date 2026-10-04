param([ValidateSet('1920x1080','1280x720')][string]$Resolution = '1920x1080', [string[]]$Panels)
# 在村庄 Play 内先 inkqa:seed；只检查显示，不保存游戏或场景。
. (Join-Path $PWD '.dsh/uni.ps1')
$motionPanels = @('role:0','role:1','role:2','role:3','role:4','role:5','role:6','role:7',
    'cultivation:0','cultivation:1','cultivation:2','alchemy','materials','field','contribution',
    'dialogue','teleport','pause','death','hud')
if ($Panels) { $motionPanels = $Panels }
$motionDimensions = $Resolution.Split('x')
foreach ($motionPanel in $motionPanels) {
    $motionResult = Uni "inkqa:$motionPanel" 30
    Write-Output $motionResult
    if ($motionResult -notmatch '^OK ') { throw "Panel unavailable: $motionPanel" }
    # 切页面后再固定 GameView，等待下一帧实际渲染尺寸稳定。
    Uni "screen:$Resolution" 30
    Start-Sleep -Milliseconds 1000
    $motionLabel = $motionPanel.Replace(':','-')
    $motionPath = Join-Path $PWD "cultivation/screenshots/InkUI-motion-panel-$motionLabel-$Resolution.png"
    Uni "shot2:$motionPath" 30
    Start-Sleep -Milliseconds 300
    $motionBytes = [System.IO.File]::ReadAllBytes($motionPath)
    $motionWidth = $motionBytes[16] * 16777216 + $motionBytes[17] * 65536 + $motionBytes[18] * 256 + $motionBytes[19]
    $motionHeight = $motionBytes[20] * 16777216 + $motionBytes[21] * 65536 + $motionBytes[22] * 256 + $motionBytes[23]
    if ($motionWidth -ne [int]$motionDimensions[0] -or $motionHeight -ne [int]$motionDimensions[1]) { throw "Wrong PNG dimensions: $motionPath" }
}
Uni 'console:errors' 30
Write-Output "Verified $($motionPanels.Count) panels at $Resolution"
