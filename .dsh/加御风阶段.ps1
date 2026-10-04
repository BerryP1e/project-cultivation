$p = "D:\project：cultivation\cultivation\Assets\Data\Tables\任务表.csv"
$enc = New-Object System.Text.UTF8Encoding($true)
$lines = [System.IO.File]::ReadAllLines($p, $enc)
$列序 = @('id','任务id','任务名','类型','阶段','阶段名','说明','条件','物品id','数量','目标npcId',
          '坐标X','坐标Y','坐标Z','到达半径','对话id','接取加标记','完成加标记','打标记','奖励物品',
          '动作','动作目标npcId','动作参数','自动接取','前置任务id','等待秒','动作速度','镜头时长','镜头高度',
          '台词','说话人','情绪','情绪强度','场景名','淡入档数','淡出档数','打字速度',
          '引导npcId','引导坐标','引导地点名','引导场景','奖励贡献')
if (($lines[0] -split ',').Count -ne $列序.Count) { throw "表头列数不是 $($列序.Count)" }
if ($lines[0] -match '38') { throw "看起来已经改过了（表头里出现 38）" }

function 造行($map) {
    $l = New-Object System.Collections.Generic.List[string]
    foreach ($名 in $列序) { if ($map.ContainsKey($名)) { $l.Add([string]$map[$名]) } else { $l.Add('') } }
    return ($l -join ',')
}

# 新阶段31「师父御风」：大师兄那套控制器里有 Yufeng_Idle 状态，师父常态御风
$御风 = 造行 @{
    id='q_main_004_31'; 任务id='q_main_004'; 任务名='拜入太虚宗'; 类型='主线'; 阶段='31'
    阶段名='师父御风'; 说明='师父踏风而至。'
    条件='等待秒数'; 等待秒='0.4'
    动作='播动画'; 动作目标npcId='npc_shifu'; 动作参数='Yufeng_Idle'
}

$out = New-Object System.Collections.Generic.List[string]
$out.Add($lines[0])
$插了 = $false
$改了 = 0
foreach ($line in $lines[1..($lines.Count - 1)]) {
    if ([string]::IsNullOrWhiteSpace($line)) { $out.Add($line); continue }
    $f = $line.Split(',')
    $row = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $列序.Count; $i++) { if ($i -lt $f.Length) { $row.Add($f[$i]) } else { $row.Add('') } }

    # 阶段 31~37 → 32~38（id 和 阶段 一起挪），给新的 31 腾位置
    $段 = 0
    if ([int]::TryParse($row[4], [ref]$段) -and $段 -ge 31 -and $段 -le 37 -and $row[1] -eq 'q_main_004') {
        $新段 = $段 + 1
        $row[0] = 'q_main_004_' + $新段
        $row[4] = [string]$新段
        $改了++
    }
    $out.Add(($row -join ','))

    # 老的 30（师父现身，生成NPC）后面插入新 31
    if ($row[1] -eq 'q_main_004' -and $row[4] -eq '31' -and -not $插了) {
        # 注意：上一行已经被改成 32 了，所以这里判断的是"刚改完的那行原本是 31"
    }
    if (-not $插了 -and $row[1] -eq 'q_main_004' -and $row[4] -eq '32' -and $row[5] -eq '师父讲塔') {
        # 把新 31 插在"原 31（现 32）"之前，也就是这一行之前 —— 得回退一步
        $out.RemoveAt($out.Count - 1)
        $out.Add($御风)
        $out.Add(($row -join ','))
        $插了 = $true
    }
}
if (-not $插了) { throw "没找到「师父讲塔」那一行，新的 31 没插进去" }
[System.IO.File]::WriteAllLines($p, $out, $enc)
Write-Output "写回 $($out.Count) 行；阶段号挪了 $改了 行；插入新阶段31「师父御风」"

$chk = [System.IO.File]::ReadAllLines($p, $enc)
Write-Output "列数分布："
$chk | ForEach-Object { ($_ -split ',').Count } | Group-Object | ForEach-Object { "  $($_.Name) 列 × $($_.Count) 行" }
Write-Output "--- 阶段 29~38 ---"
$chk | Where-Object { $_ -match '^q_main_004_3[0-8]?,' -or $_ -match '^q_main_004_29,' } | ForEach-Object {
    $f = $_ -split ','; "  阶段{0,2} {1,-14} 条件={2,-8} 动作={3,-8} 参数={4}" -f $f[4], $f[5], $f[7], $f[20], $f[22]
}
