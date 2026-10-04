param([switch]$FixBom)
# ASCII-only. Full doc-set health check. Re-runnable.
$ErrorActionPreference = 'Stop'
$rootFull = (Resolve-Path -LiteralPath 'docs').Path

$all = Get-ChildItem -Path 'docs' -Recurse -File -Filter *.md | Where-Object { $_.FullName -notmatch '\\archive\\' -and $_.FullName -notmatch '_rb[0-9]|_bak|_p[0-9]|_rebuild' }
$arch = Get-ChildItem -Path 'docs' -Recurse -File -Filter *.md | Where-Object { $_.FullName -match '\\archive\\' }

"docs live files : $($all.Count)   archive files: $($arch.Count)"
$liveBytes = ($all | Measure-Object Length -Sum).Sum
$archBytes = ($arch | Measure-Object Length -Sum).Sum
"live bytes      : $liveBytes"
"archive bytes   : $archBytes"
""

# ---- 1) dead links ----
$dead = 0; $total = 0; $deadList = @()
foreach ($f in $all) {
  $t = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
  $dir = Split-Path $f.FullName -Parent
  foreach ($m in [regex]::Matches($t, '\]\(([^)]+)\)')) {
    $tgt = $m.Groups[1].Value.Trim()
    if ($tgt -match '^(https?:|mailto:|#)') { continue }
    $path = ($tgt -split '#')[0]
    if ([string]::IsNullOrWhiteSpace($path)) { continue }
    $total++
    if (-not (Test-Path -LiteralPath (Join-Path $dir $path))) {
      $dead++; $deadList += ($f.FullName.Substring($rootFull.Length).TrimStart('\','/') + '  ->  ' + $tgt)
    }
  }
}
"[LINKS] checked $total   DEAD $dead"
$deadList | ForEach-Object { "        $_" }

# ---- 2) malformed links ----
$mal = 0
foreach ($f in $all) {
  foreach ($ln in [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)) {
    if ($ln -match '\]\(') {
      $s = [regex]::Replace($ln, '\[[^\[\]]*\]\([^)]*\)', '')
      if ($s -match '\]\(') { $mal++; "        MALFORMED $($f.Name): $($ln.Trim())" }
    }
  }
}
"[LINKS] malformed $mal"

# ---- 3) control chars / mojibake ----
$ctl = 0; $moji = 0
foreach ($f in $all) {
  $t = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
  $c = [regex]::Matches($t, '[\u0000-\u0008\u000B\u000C\u000E-\u001F]').Count
  if ($c) { $ctl += $c; "        CONTROL $($f.FullName.Substring($rootFull.Length)) x$c" }
  if ($t.Contains([string]([char]0x9286))) { $moji++; "        MOJIBAKE $($f.FullName.Substring($rootFull.Length))" }
}
"[TEXT ] control chars $ctl   mojibake files $moji"

# ---- 4) BOM ----
$noBom = @()
foreach ($f in ($all + $arch)) {
  $b = [System.IO.File]::ReadAllBytes($f.FullName)
  if (-not ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)) { $noBom += $f }
}
if ($FixBom -and $noBom.Count -gt 0) {
  foreach ($f in $noBom) {
    $t = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($f.FullName, $t, (New-Object System.Text.UTF8Encoding($true)))
  }
  "[BOM  ] added to $($noBom.Count) files"
  $noBom = @()
}
"[BOM  ] without BOM: $($noBom.Count)"
$noBom | ForEach-Object { "        NOBOM $($_.FullName.Substring($rootFull.Length))" }

# ---- 5) nav header / H1 ----
$noNav = @(); $noH1 = @()
foreach ($f in $all) {
  $L = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
  if (-not ($L.Count -gt 0 -and $L[0] -match '^#\s')) { $noH1 += $f.FullName.Substring($rootFull.Length) }
  $has = $false
  for ($i = 0; $i -lt [Math]::Min(16, $L.Count); $i++) { if ($L[$i] -match '^> \*\*') { $has = $true; break } }
  if (-not $has) { $noNav += $f.FullName.Substring($rootFull.Length) }
}
"[DOC  ] missing H1: $($noH1.Count)   missing nav header: $($noNav.Count)"
$noNav | ForEach-Object { "        NONAV $_" }

# ---- 6) stray temp dirs ----
$tmp = Get-ChildItem -Path 'docs' -Directory | Where-Object { $_.Name -match '^_' }
"[TEMP ] leftover dirs: $($tmp.Count)  $(($tmp | ForEach-Object { $_.Name }) -join ', ')"
"=== END ==="
