param(
  [Parameter(Mandatory=$true)][string]$New,
  [Parameter(Mandatory=$true)][string]$Source,
  [int]$From = 1,
  [int]$To = 0,
  [int]$MaxList = 60
)

# ASCII-only helper. Compares "hard facts" (backticked identifiers, asset paths,
# file names, and numbers) between a rewritten doc and its source text.
# Usage:
#   .dsh\loss_check.ps1 -New docs\architecture\敌人AI.md -Source docs\ai\archive\开发注意事项-流水原文.md -From 1516 -To 2208

$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$p) {
  $full = (Resolve-Path -LiteralPath $p).Path
  return [System.IO.File]::ReadAllText($full, [System.Text.Encoding]::UTF8)
}

function Get-Ids([string]$t) {
  $set = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($m in [regex]::Matches($t, '`([^`\r\n]{3,160})`')) { [void]$set.Add($m.Groups[1].Value.Trim()) }
  foreach ($m in [regex]::Matches($t, 'Assets/[A-Za-z0-9_\.\-/\(\)\u4e00-\u9fff]+')) { [void]$set.Add($m.Value) }
  foreach ($m in [regex]::Matches($t, '[A-Za-z_][A-Za-z0-9_]*\.(cs|csv|prefab|asset|shader|unity|scene|controller|fbx|FBX|mat|png|txt|pdf)')) { [void]$set.Add($m.Value) }
  return $set
}

function Get-Nums([string]$t) {
  $set = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($m in [regex]::Matches($t, '(?<![\d\.])\d+(?:\.\d+)?')) { [void]$set.Add($m.Value) }
  return $set
}

$newText = Read-Utf8 $New
$srcAll = (Read-Utf8 $Source) -split "`r?`n"
if ($To -le 0 -or $To -gt $srcAll.Count) { $To = $srcAll.Count }
$srcText = ($srcAll[($From - 1)..($To - 1)]) -join "`n"

$newIds = Get-Ids $newText
$srcIds = Get-Ids $srcText
$newNums = Get-Nums $newText
$srcNums = Get-Nums $srcText

$missingIds = @($srcIds | Where-Object { -not $newIds.Contains($_) } | Sort-Object)
$missingNums = @($srcNums | Where-Object { -not $newNums.Contains($_) } | Sort-Object)

"=== LOSSLESS CHECK ==="
"new    : $New  ($($newText.Length) chars)"
"source : $Source  lines $From..$To  ($($srcText.Length) chars)"
""
"identifiers : source $($srcIds.Count)  ->  new $($newIds.Count)   missing $($missingIds.Count)"
"numbers     : source $($srcNums.Count)  ->  new $($newNums.Count)   missing $($missingNums.Count)"
""
if ($missingIds.Count -gt 0) {
  "--- identifiers present in SOURCE but not in NEW (first $MaxList) ---"
  $missingIds | Select-Object -First $MaxList | ForEach-Object { "  [id] $_" }
  ""
}
if ($missingNums.Count -gt 0) {
  "--- numbers present in SOURCE but not in NEW (first $MaxList) ---"
  $missingNums | Select-Object -First $MaxList | ForEach-Object { "  [num] $_" }
}
"=== END ==="
