# 只读取 PNG 头，不缩放截图；从仓库根执行。
$labels = @('神通','背包','境界','法宝','灵阵','战阵','坐骑','外观','闭关修炼','境界突破','转修功法','丹房','药材选择','灵田总览','灵田地块','灵田生长中','灵田成熟','功德堂','对话','传送','传送宗门','镇妖塔HUD','镇妖塔死亡选择','HUD','Toast','暂停','身陨','起名','主菜单','存档选择')
$failures = 0
$rows = foreach ($label in $labels) {
    foreach ($phase in @('前','后')) {
        foreach ($resolution in @('1920x1080','1280x720')) {
            $path = Join-Path $PWD "cultivation/screenshots/UI_${label}_${phase}_${resolution}.png"
            $ok = Test-Path -LiteralPath $path
            $actual = 'missing'
            if ($ok) {
                $stream = [IO.File]::OpenRead($path)
                try { $header = New-Object byte[] 24; $read = $stream.Read($header, 0, 24) } finally { $stream.Dispose() }
                $widthBytes = [byte[]]$header[16..19]; $heightBytes = [byte[]]$header[20..23]
                [Array]::Reverse($widthBytes); [Array]::Reverse($heightBytes)
                $actual = "{0}x{1}" -f [BitConverter]::ToInt32($widthBytes,0), [BitConverter]::ToInt32($heightBytes,0)
                $ok = $read -eq 24 -and $header[0] -eq 137 -and $header[1] -eq 80 -and $actual -eq $resolution
            }
            if (!$ok) { $failures++; Write-Warning "$label $phase $resolution actual=$actual" }
            [pscustomobject]@{ Panel=$label; Phase=$phase; Expected=$resolution; Actual=$actual; Pass=$ok; Path=$path }
        }
    }
}
$rows | Export-Csv -LiteralPath 'cultivation/screenshots/InkUI-capture-manifest.csv' -NoTypeInformation -Encoding utf8BOM
"Captures=$($rows.Count); MissingOrWrongSize=$failures"
if ($failures) { exit 1 }
