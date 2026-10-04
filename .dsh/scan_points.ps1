param(
  [string]$SceneGlob = 'cultivation\Assets\Scenes\*.scene',
  [string]$Out = '.dsh/scene_points.txt'
)
# ASCII-only, read-only. For each scene, list the Transform world-ish local position of
# GameObjects whose name matches a few key names (Player / SpawnPoint / cameras / bounds),
# plus whether the object is active. Helps diagnose "player spawned in a dead zone".
$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$p) { return [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $p).Path, [System.Text.Encoding]::UTF8) }
function Dec([string]$s) { return [regex]::Replace($s, '\\u([0-9A-Fa-f]{4})', { param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) }) }

$want = 'Player|SpawnPoint|Main Camera|Boundary|cultivation room|CameraRig'
$sb = New-Object System.Text.StringBuilder

foreach ($f in (Get-ChildItem -Path $SceneGlob)) {
  $t = Read-Utf8 $f.FullName
  # map fileID -> name for GameObjects
  $goName = @{}
  $goActive = @{}
  foreach ($m in [regex]::Matches($t, '(?s)--- !u!1 &(\d+)\r?\n(.*?)(?=\r?\n--- |\z)')) {
    $b = $m.Groups[2].Value
    $nm = Dec ([regex]::Match($b, 'm_Name: (.*)').Groups[1].Value.Trim())
    $act = [regex]::Match($b, 'm_IsActive: (\d)').Groups[1].Value
    $goName[$m.Groups[1].Value] = $nm
    $goActive[$m.Groups[1].Value] = $act
  }
  # map Transform fileID -> (goId, localPosition)
  $rows = @()
  foreach ($m in [regex]::Matches($t, '(?s)--- !u!4 &(\d+)\r?\n(.*?)(?=\r?\n--- |\z)')) {
    $b = $m.Groups[2].Value
    $goId = [regex]::Match($b, 'm_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    if (-not $goName.ContainsKey($goId)) { continue }
    $nm = $goName[$goId]
    if ($nm -notmatch $want) { continue }
    $p = [regex]::Match($b, 'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}')
    $s = [regex]::Match($b, 'm_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}')
    $rows += ("  {0,-22} active={1}  pos=({2}, {3}, {4})   scale=({5}, {6}, {7})" -f `
      $nm, $goActive[$goId], $p.Groups[1].Value, $p.Groups[2].Value, $p.Groups[3].Value,
      $s.Groups[1].Value, $s.Groups[2].Value, $s.Groups[3].Value)
  }
  [void]$sb.AppendLine("########## " + $f.Name)
  $rows | Sort-Object -Unique | ForEach-Object { [void]$sb.AppendLine($_) }
  [void]$sb.AppendLine("")
}

[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Out), $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
"written: $Out"
