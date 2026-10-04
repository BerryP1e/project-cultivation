$p = "D:\project：cultivation\cultivation\Assets\Data\Tables\任务表.csv"
$enc = New-Object System.Text.UTF8Encoding($true)
$lines = [System.IO.File]::ReadAllLines($p, $enc)

$旧列数 = 37
$head = ($lines[0] -split ',')
if ($head.Count -ne $旧列数) { throw "表头列数不是 $旧列数，实际 $($head.Count) —— 先确认表结构再跑" }
if ($head -contains '引导npcId') { throw "已经加过引导列了，别重复跑" }

# ---------- 工具：把一行拆成正好 37 列 ----------
function 归一($line) {
    $f = $line.Split(',')
    $list = New-Object System.Collections.Generic.List[string]
    for ($c = 0; $c -lt $旧列数; $c++) {
        if ($c -lt $f.Length) { $list.Add($f[$c]) } else { $list.Add('') }
    }
    return $list
}

# ---------- 工具：造一行（按 列名 → 值 的字典填，其余留空） ----------
$列序 = @('id','任务id','任务名','类型','阶段','阶段名','说明','条件','物品id','数量','目标npcId',
          '坐标X','坐标Y','坐标Z','到达半径','对话id','接取加标记','完成加标记','打标记','奖励物品',
          '动作','动作目标npcId','动作参数','自动接取','前置任务id','等待秒','动作速度','镜头时长','镜头高度',
          '台词','说话人','情绪','情绪强度','场景名','淡入档数','淡出档数','打字速度',
          '引导npcId','引导坐标','引导地点名','引导场景')
function 造行($map) {
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($名 in $列序) {
        $v = ''
        if ($map.ContainsKey($名)) { $v = [string]$map[$名] }
        $list.Add($v)
    }
    return ($list -join ',')
}

# ---------- ① 阶段25 重写成「黑幕 → 切场景」的第一步 ----------
$行25 = @{
    'id'='q_main_004_25'; '任务id'='q_main_004'; '任务名'='拜入太虚宗'; '类型'='主线'; '阶段'='25'
    '阶段名'='翌日启程'; '说明'='过了一夜，大师兄带你去宗门丹房。'
    '条件'='黑幕落下完成'; '动作'='黑幕字幕'
    '台词'='翌日，太虚宗|大师兄引你往丹房去'
    '淡入档数'='24'; '淡出档数'='24'
}
# ---------- ② 新增 阶段26 切场景 / 阶段27 走到丹房 ----------
$行26 = @{
    'id'='q_main_004_26'; '任务id'='q_main_004'; '任务名'='拜入太虚宗'; '类型'='主线'; '阶段'='26'
    '阶段名'='到宗门丹房'; '说明'='黑幕之后直接落在宗门丹房外。'
    '条件'='无'; '动作'='切换场景'; '场景名'='Sect'
    '坐标X'='2.3'; '坐标Y'='2'; '坐标Z'='46.55'
}
$行27 = @{
    'id'='q_main_004_27'; '任务id'='q_main_004'; '任务名'='拜入太虚宗'; '类型'='主线'; '阶段'='27'
    '阶段名'='走近丹房'; '说明'='丹房就在前面——走近按 F，打开丹炉试试炼丹。'
    '条件'='到达'; '到达半径'='5'
    '坐标X'='-4.59'; '坐标Y'='3.48'; '坐标Z'='29.92'
    '完成加标记'='q_主线_丹房已解锁'
    '引导坐标'='-4.59,3.48,29.92'; '引导地点名'='宗门炼丹阁'; '引导场景'='Sect'
}

# ---------- ③ 自动推不出来的阶段，补引导 ----------
$引导 = @{
    'q_main_004_15' = @('', '-18.23,0.5,27.54', '洞府传送点', 'Sect')   # 「跟上大师兄」：目标就是那个光圈
}

$out = New-Object System.Collections.Generic.List[string]
$out.Add($lines[0] + ',引导npcId,引导坐标,引导地点名,引导场景')

for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ([string]::IsNullOrWhiteSpace($line)) { $out.Add($line); continue }

    $f = 归一 $line
    $id = $f[0]

    if ($id -eq 'q_main_004_25') {
        $out.Add((造行 $行25))
        $out.Add((造行 $行26))
        $out.Add((造行 $行27))
        continue
    }

    $g = $引导[$id]
    if ($g) { $f.AddRange([string[]]$g) } else { $f.Add(''); $f.Add(''); $f.Add(''); $f.Add('') }
    $out.Add(($f -join ','))
}

[System.IO.File]::WriteAllLines($p, $out, $enc)
Write-Output "已写回：$($out.Count) 行（原 $($lines.Count) 行，新增 2 行）"
Write-Output "表头 = $($out[0])"
