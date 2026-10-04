param(
  [string]$SceneGlob = 'cultivation\Assets\Scenes\*.scene',
  [string]$Out = '.dsh/stations_dump.txt'
)
# ASCII-only, read-only. Dumps every StationInteractable-like MonoBehaviour
# (identified by the escaped field name \u754C\u9762\u9884\u5236\u4F53 = "interface prefab")
# into a UTF-8 text file so the caller can read it without console codepage issues.
$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$p) { return [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $p).Path, [System.Text.Encoding]::UTF8) }
function Dec([string]$s) { return [regex]::Replace($s, '\\u([0-9A-Fa-f]{4})', { param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) }) }

$marker = '\u754C\u9762\u9884\u5236\u4F53'
$sb = New-Object System.Text.StringBuilder

foreach ($f in (Get-ChildItem -Path $SceneGlob)) {
  $t = Read-Utf8 $f.FullName
  $goBlocks = [regex]::Matches($t, '(?s)--- !u!1 &(\d+)\r?\n(.*?)(?=\r?\n--- |\z)')
  $names = @{}
  foreach ($m in $goBlocks) {
    $nm = [regex]::Match($m.Groups[2].Value, 'm_Name: (.*)').Groups[1].Value.Trim()
    $names[$m.Groups[1].Value] = (Dec $nm)
  }
  [void]$sb.AppendLine("########## " + $f.Name + "  | GameObjects=" + $goBlocks.Count + " | names=" + $names.Count)

  $n = 0
  foreach ($m in [regex]::Matches($t, '(?s)--- !u!114 &(\d+)\r?\n(.*?)(?=\r?\n--- |\z)')) {
    $body = $m.Groups[2].Value
    if ($body -notmatch [regex]::Escape($marker)) { continue }
    $n++
    $goId = [regex]::Match($body, 'm_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    $goName = if ($names.ContainsKey($goId)) { $names[$goId] } else { "?" + $goId }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("--- STATION " + $n + " : " + $goName)
    $dec = Dec $body
    foreach ($line in ($dec -split "`r?`n")) {
      if ($line -match '^\s+m_') { continue }
      if ($line -match '^\s*$') { continue }
      if ($line -match '^\s*MonoBehaviour:') { continue }
      [void]$sb.AppendLine("    " + $line.TrimEnd())
    }
  }
  [void]$sb.AppendLine("(stations found: " + $n + ")")
  [void]$sb.AppendLine("")
}

[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Out), $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
"written: $Out"
