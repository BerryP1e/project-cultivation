param([string]$DocsRoot = 'docs')
# ASCII-only. Reports markdown links inside docs/ that point at a missing file.
$ErrorActionPreference = 'Stop'
$rootFull = (Resolve-Path -LiteralPath $DocsRoot).Path
$files = Get-ChildItem -Path $DocsRoot -Recurse -File -Filter *.md |
         Where-Object { $_.FullName -notmatch '_rebuild|_rb[0-9]|_bak|_p[0-9]' }
$dead = 0; $total = 0
foreach ($f in $files) {
  $t = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
  $dir = Split-Path $f.FullName -Parent
  foreach ($m in [regex]::Matches($t, '\]\(([^)]+)\)')) {
    $tgt = $m.Groups[1].Value.Trim()
    if ($tgt -match '^(https?:|mailto:|#)') { continue }
    $path = ($tgt -split '#')[0]
    if ([string]::IsNullOrWhiteSpace($path)) { continue }
    $total++
    $full = Join-Path $dir $path
    if (-not (Test-Path -LiteralPath $full)) {
      $dead++
      "DEAD  $($f.FullName.Substring($rootFull.Length).TrimStart('\','/'))  ->  $tgt"
    }
  }
}
"---"
"links checked: $total   dead: $dead"
