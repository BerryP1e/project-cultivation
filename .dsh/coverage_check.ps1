param(
  [string]$Archive = 'docs\ai\archive\开发注意事项-流水原文.md',
  [string]$DocsRoot = 'docs',
  [int]$MinIds = 8,
  [int]$MaxList = 8
)

# ASCII-only. For every H1 section of the archived dev-log, measure how many of its
# "hard fact" identifiers (backticked spans / asset paths / file names) also appear
# somewhere in the LIVE docs. Low coverage => that section's content is probably
# not represented anywhere yet.

$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$p) {
  return [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $p).Path, [System.Text.Encoding]::UTF8)
}

function Get-Ids([string]$t) {
  $set = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($m in [regex]::Matches($t, '`([^`\r\n]{4,120})`')) { [void]$set.Add($m.Groups[1].Value.Trim()) }
  foreach ($m in [regex]::Matches($t, 'Assets/[A-Za-z0-9_\.\-/\(\)\u4e00-\u9fff]{4,120}')) { [void]$set.Add($m.Value) }
  foreach ($m in [regex]::Matches($t, '[A-Za-z_][A-Za-z0-9_]{2,}\.(cs|csv|prefab|asset|shader|unity|scene|controller|mat)')) { [void]$set.Add($m.Value) }
  return $set
}

# ---- live docs corpus ----
$live = Get-ChildItem -Path $DocsRoot -Recurse -File -Filter *.md |
        Where-Object { $_.FullName -notmatch '\\archive\\' -and $_.FullName -notmatch '_rebuild|_rb[0-9]|_bak' }
$corpus = ''
foreach ($f in $live) { $corpus += (Read-Utf8 $f.FullName) + "`n" }
$liveIds = Get-Ids $corpus

# ---- archive sections ----
$lines = [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $Archive).Path, [System.Text.Encoding]::UTF8)
$starts = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match '^#{1,2}\s+\S') { $starts += [pscustomobject]@{ Line = $i; Title = $lines[$i] } }
}

"live docs: $($live.Count) files, $($liveIds.Count) distinct identifiers"
"archive H1 sections: $($starts.Count)"
""
"{0,-52} {1,6} {2,6} {3,7}" -f 'ARCHIVE SECTION', 'ids', 'hit', 'cover'
"-" * 78

$report = @()
for ($s = 0; $s -lt $starts.Count; $s++) {
  $from = $starts[$s].Line
  $to = if ($s + 1 -lt $starts.Count) { $starts[$s + 1].Line - 1 } else { $lines.Count - 1 }
  $text = ($lines[$from..$to]) -join "`n"
  $ids = Get-Ids $text
  if ($ids.Count -lt $MinIds) { continue }
  $hit = @($ids | Where-Object { $liveIds.Contains($_) })
  $cover = [math]::Round(100.0 * $hit.Count / $ids.Count, 1)
  $miss = @($ids | Where-Object { -not $liveIds.Contains($_) })
  $title = $starts[$s].Title
  if ($title.Length -gt 50) { $title = $title.Substring(0, 50) }
  "{0,-52} {1,6} {2,6} {3,6}%" -f $title, $ids.Count, $hit.Count, $cover
  $report += [pscustomobject]@{ Title = $title; Cover = $cover; Miss = $miss; Ids = $ids.Count }
}

""
"=== sections with coverage < 60% (candidate MISSING content) ==="
foreach ($r in ($report | Where-Object { $_.Cover -lt 60 } | Sort-Object Cover)) {
  ""
  "--- $($r.Title)   cover $($r.Cover)%  ($($r.Ids) ids)"
  $r.Miss | Select-Object -First $MaxList | ForEach-Object { "    $_" }
}
