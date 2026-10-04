$p = "D:\project：cultivation\cultivation\Assets\Data\Tables\任务表.csv"
$enc = New-Object System.Text.UTF8Encoding($true)
$lines = [System.IO.File]::ReadAllLines($p, $enc)
$列数 = 41
if (($lines[0] -split ',').Count -ne $列数) { throw "表头不是 $列数 列，先确认" }

# id → 引导四列（引导npcId / 引导坐标 / 引导地点名 / 引导场景）
# ★ 坐标里**不能用半角逗号** —— CSV 是按逗号切的，一个字段里带逗号会把后面所有列顶偏一格！
#   这里用分号，QuestDefinition 那边认分号/空格/全角逗号。
$引导 = @{
    'q_main_004_15' = @('', '-18.23;0.5;27.54', '洞府传送点', 'Sect')
    'q_main_004_27' = @('', '-4.59;3.48;29.92', '宗门炼丹阁', 'Sect')
}

$out = New-Object System.Collections.Generic.List[string]
$out.Add($lines[0])

for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ([string]::IsNullOrWhiteSpace($line)) { $out.Add($line); continue }

    $f = $line.Split(',')
    $row = New-Object System.Collections.Generic.List[string]
    for ($c = 0; $c -lt $列数; $c++) {
        if ($c -lt $f.Length) { $row.Add($f[$c]) } else { $row.Add('') }
    }
    # 超出 41 的（带逗号的脏值）已经在上面被截掉，这里按 id 重写引导四列
    $id = $row[0]
    $g = $引导[$id]
    if ($g -eq $null) { $g = @('', '', '', '') }
    $row[37] = $g[0]; $row[38] = $g[1]; $row[39] = $g[2]; $row[40] = $g[3]

    $out.Add(($row -join ','))
}

[System.IO.File]::WriteAllLines($p, $out, $enc)
Write-Output "写回完成：$($out.Count) 行"
$check = [System.IO.File]::ReadAllLines($p, $enc)
Write-Output "列数分布："
$check | ForEach-Object { ($_ -split ',').Count } | Group-Object | ForEach-Object { "  $($_.Name) 列 × $($_.Count) 行" }
Write-Output "`n=== 15 / 27 行 ==="
$check | Where-Object { $_ -like 'q_main_004_15,*' -or $_ -like 'q_main_004_27,*' } | ForEach-Object { $_ }
