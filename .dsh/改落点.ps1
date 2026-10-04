$p = "D:\project：cultivation\cultivation\Assets\Data\Tables\任务表.csv"
$enc = New-Object System.Text.UTF8Encoding($true)
$lines = [System.IO.File]::ReadAllLines($p, $enc)
$out = New-Object System.Collections.Generic.List[string]
$改成 = $false
foreach ($line in $lines) {
    if ($line -like 'q_main_004_26,*') {
        $f = $line.Split(',')
        # 11/12/13 = 坐标X/Y/Z。原值 (2.3, 2, 46.55) 那块石头站不住（实测玩家掉到 y=-12），
        # 换成洞府外那片**平坦地板**上的点：(-0.76, 2, 39.16)
        $f[11] = '-0.76'; $f[12] = '2'; $f[13] = '39.16'
        $out.Add(($f -join ','))
        $改成 = $true
        Write-Output ("已改阶段26落点 → " + ($f[11..13] -join ', '))
    } else { $out.Add($line) }
}
if (-not $改成) { throw "没找到 q_main_004_26 那一行" }
[System.IO.File]::WriteAllLines($p, $out, $enc)
Write-Output "写回 $($out.Count) 行"
