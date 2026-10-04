param(
  [string]$DocsRoot = 'docs',
  [string]$MapFile  = '.dsh/linkmap.tsv',
  [switch]$Apply
)

# ASCII-only. Rewrites references to renamed/merged/split docs so every link points at
# the new file (relative path recomputed from the linking file's own directory).
# Without -Apply it only reports.

$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$p) { return [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $p).Path, [System.Text.Encoding]::UTF8) }
function Write-Utf8Bom([string]$p, [string]$t) { [System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $p).Path, $t, (New-Object System.Text.UTF8Encoding($true))) }

function RelPath([string]$fromDirRel, [string]$toRel) {
  # both are '/'-separated paths relative to the docs root. fromDirRel may be ''.
  $from = if ([string]::IsNullOrEmpty($fromDirRel)) { @() } else { $fromDirRel -split '/' }
  $to = $toRel -split '/'
  $i = 0
  while ($i -lt $from.Count -and $i -lt ($to.Count - 1) -and $from[$i] -eq $to[$i]) { $i++ }
  $parts = @()
  for ($k = 0; $k -lt ($from.Count - $i); $k++) { $parts += '..' }
  for ($k = $i; $k -lt $to.Count; $k++) { $parts += $to[$k] }
  return ($parts -join '/')
}

# ---- load map ----
$rows = @()
foreach ($line in ([System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $MapFile).Path, [System.Text.Encoding]::UTF8))) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $c = $line -split "`t"
  if ($c.Count -lt 2) { continue }
  $old = $c[0].Trim()
  $news = @()
  for ($i = 1; $i -lt $c.Count; $i++) { if (-not [string]::IsNullOrWhiteSpace($c[$i])) { $news += $c[$i].Trim() } }
  $rows += [pscustomobject]@{
    OldBase = [System.IO.Path]::GetFileNameWithoutExtension($old)
    OldPath = $old
    New     = $news
  }
}

# ---- files to scan ----
$files = Get-ChildItem -Path $DocsRoot -Recurse -File -Filter *.md |
         Where-Object { $_.FullName -notmatch '\\archive\\' -and $_.FullName -notmatch '_rebuild|_rb[0-9]|_bak|_p[0-9]' }

$docsRootFull = (Resolve-Path -LiteralPath $DocsRoot).Path
$oldRelSet = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($r in $rows) { [void]$oldRelSet.Add($r.OldPath) }
$totalChanges = 0

foreach ($f in $files) {
  $rel = $f.FullName.Substring($docsRootFull.Length).TrimStart('\', '/').Replace('\', '/')
  if ($oldRelSet.Contains($rel)) { continue }   # this file is being deleted anyway
  $dirRel = if ($rel.Contains('/')) { $rel.Substring(0, $rel.LastIndexOf('/')) } else { '' }
  $text = Read-Utf8 $f.FullName
  $before = $text

  foreach ($r in $rows) {
    $ob = [regex]::Escape($r.OldBase)
    # 1) markdown link target: ]( ..../OLD.md#anchor )
    $text = [regex]::Replace($text, '\]\(\s*(?:[^)\s]*/)?' + $ob + '\.md(#[^)\s]*)?\s*\)', {
      param($m)
      $suf = $m.Groups[1].Value
      $t1 = RelPath $dirRel $r.New[0]
      if ($r.New.Count -gt 1) {
        $b2 = [System.IO.Path]::GetFileNameWithoutExtension($r.New[1])
        $t2 = RelPath $dirRel $r.New[1]
        return '](' + $t1 + $suf + ')' + [char]0x3001 + '[' + $b2 + '](' + $t2 + $suf + ')'
      }
      return '](' + $t1 + $suf + ')'
    })
    # 1b) link TEXT still naming the old doc
    $newTitle = if ($r.New.Count -gt 1) { [System.IO.Path]::GetFileNameWithoutExtension($r.New[0]) } else { [System.IO.Path]::GetFileNameWithoutExtension($r.New[0]) }
    $text = [regex]::Replace($text, '\[([^\]]*?)' + $ob + '([^\]]*?)\]\(',
      ('[$1' + $newTitle + '$2]('))
    # 2) plain/backticked mention of OLD.md (with optional dir prefix)
    $text = [regex]::Replace($text, '(?:[A-Za-z0-9_\-\.]+/)*' + $ob + '\.md', {
      param($m)
      $t1 = RelPath $dirRel $r.New[0]
      if ($r.New.Count -gt 1) { return $t1 + ' / ' + (RelPath $dirRel $r.New[1]) }
      return $t1
    })
  }

  if ($text -ne $before) {
    $totalChanges++
    "MOD  $rel"
    if ($Apply) { Write-Utf8Bom $f.FullName $text }
  }
}

"---"
"files changed: $totalChanges   (Apply=$Apply)"
