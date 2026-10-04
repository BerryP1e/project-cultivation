$root = "D:\project：cultivation\cultivation\Assets\Data\Tables"
$enc = New-Object System.Text.UTF8Encoding($true)

# ---------- ① 物品表：加「兑换消耗贡献」列 + 给"没有别的来源"的东西定价 ----------
$p = "$root\物品表.csv"
$lines = [System.IO.File]::ReadAllLines($p, $enc)
if (($lines[0] -split ',') -contains '兑换消耗贡献') { throw "物品表已经加过这列了" }
$物品列数 = ($lines[0] -split ',').Count

# 定价：功德堂卖的就是这些"别处拿不到"的东西（灵田三件套 + 灵种）
$价 = @{
    'item_lingtian_kaituo' = 30    # 灵田开拓令
    'item_lingtian_ling'   = 60    # 灵田升阶令
    'item_muzhuang'        = 20    # 练功木桩
    'item_seed_shengling'  = 5
    'item_seed_ninglu'     = 8
    'item_seed_huichun'    = 8
    'item_seed_qingxin'    = 15
    'item_seed_chiyan'     = 25
}
$out = New-Object System.Collections.Generic.List[string]
$out.Add($lines[0] + ',兑换消耗贡献')
$定 = 0
foreach ($line in $lines[1..($lines.Count - 1)]) {
    if ([string]::IsNullOrWhiteSpace($line)) { $out.Add($line); continue }
    $f = $line.Split(',')
    $row = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $物品列数; $i++) { if ($i -lt $f.Length) { $row.Add($f[$i]) } else { $row.Add('') } }
    $id = $row[0]
    if ($价.ContainsKey($id)) { $row.Add([string]$价[$id]); $定++ }
    else { $row.Add('') }
    $out.Add(($row -join ','))
}
[System.IO.File]::WriteAllLines($p, $out, $enc)
Write-Output "物品表：加了一列，定价 $定 件"

# ---------- ② 任务表：加「奖励贡献」列 + 给几个阶段发贡献 ----------
$p2 = "$root\任务表.csv"
$lines2 = [System.IO.File]::ReadAllLines($p2, $enc)
if (($lines2[0] -split ',') -contains '奖励贡献') { throw "任务表已经加过这列了" }
$任务列数 = ($lines2[0] -split ',').Count
$贡献 = @{
    'q_main_001_4'   = 10     # 初入山门收尾
    'q_main_004_22'  = 20     # 灵田已开
    'q_main_004_27'  = 30     # 走近丹房
    'q_main_004_34'  = 50     # 走到塔门（解锁镇妖塔）
    'q_main_004_35'  = 30     # 日常修行
    'q_main_004_36'  = 20     # 灵田的异样
    'q_main_004_37'  = 50     # 塔中的共鸣
}
$out2 = New-Object System.Collections.Generic.List[string]
$out2.Add($lines2[0] + ',奖励贡献')
$发 = 0
foreach ($line in $lines2[1..($lines2.Count - 1)]) {
    if ([string]::IsNullOrWhiteSpace($line)) { $out2.Add($line); continue }
    $f = $line.Split(',')
    $row = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $任务列数; $i++) { if ($i -lt $f.Length) { $row.Add($f[$i]) } else { $row.Add('') } }
    $id = $row[0]
    if ($贡献.ContainsKey($id)) { $row.Add([string]$贡献[$id]); $发++ }
    else { $row.Add('') }
    $out2.Add(($row -join ','))
}
[System.IO.File]::WriteAllLines($p2, $out2, $enc)
Write-Output "任务表：加了一列，$发 个阶段发贡献"

# ---------- 校验 ----------
foreach ($pair in @(@("物品表", "$root\物品表.csv", 40), @("任务表", "$root\任务表.csv", 42))) {
    $chk = [System.IO.File]::ReadAllLines($pair[1], $enc)
    Write-Output ("{0}：表头 {1} 列（应 {2}），数据行 {3}" -f $pair[0], ($chk[0] -split ',').Count, $pair[2], ($chk.Count - 1))
    $chk | ForEach-Object { ($_ -split ',').Count } | Group-Object | ForEach-Object { "    $($_.Name) 列 × $($_.Count) 行" }
}
