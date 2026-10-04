param(
  [Parameter(Mandatory=$true)][string[]]$New,
  [Parameter(Mandatory=$true)][string[]]$Source,
  [int]$MaxList = 40
)

# ASCII-only. Multi-file variant: reports identifiers/numbers present in ANY -Source
# file but in NONE of the -New files. Use this to gate deletions after a merge/split.

$ErrorActionPreference = 'Stop'

function Read-Utf8([string]$p) {
  return [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $p).Path, [System.Text.Encoding]::UTF8)
}

function Get-Ids([string]$t) {
  $set = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($m in [regex]::Matches($t, '`([^`\r\n]{4,160})`')) { [void]$set.Add($m.Groups[1].Value.Trim()) }
  foreach ($m in [regex]::Matches($t, 'Assets/[A-Za-z0-9_\.\-/\(\)\u4e00-\u9fff]{4,140}')) { [void]$set.Add($m.Value) }
  foreach ($m in [regex]::Matches($t, '[A-Za-z_][A-Za-z0-9_]{2,}\.(cs|csv|prefab|asset|shader|unity|scene|controller|mat|png)')) { [void]$set.Add($m.Value) }
  return $set
}

function Get-Nums([string]$t) {
  $set = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($m in [regex]::Matches($t, '(?<![\d\.])\d+(?:\.\d+)?')) { [void]$set.Add($m.Value) }
  return $set
}

$newIds = New-Object 'System.Collections.Generic.HashSet[string]'
$newNums = New-Object 'System.Collections.Generic.HashSet[string]'
$newChars = 0
foreach ($n in $New) {
  $t = Read-Utf8 $n
  $newChars += $t.Length
  foreach ($x in (Get-Ids $t)) { [void]$newIds.Add($x) }
  foreach ($x in (Get-Nums $t)) { [void]$newNums.Add($x) }
}

$srcIds = New-Object 'System.Collections.Generic.HashSet[string]'
$srcNums = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($s in $Source) {
  $t = Read-Utf8 $s
  foreach ($x in (Get-Ids $t)) { [void]$srcIds.Add($x) }
  foreach ($x in (Get-Nums $t)) { [void]$srcNums.Add($x) }
}

$missIds = @($srcIds | Where-Object { -not $newIds.Contains($_) } | Sort-Object)
$missNums = @($srcNums | Where-Object { -not $newNums.Contains($_) } | Sort-Object)

"=== GROUP LOSSLESS CHECK ==="
"NEW    : " + ($New -join '  +  ')
"SOURCE : " + ($Source -join '  +  ')
"chars  : source-docs vs new-docs  new=$newChars"
"ids    : source $($srcIds.Count) -> new $($newIds.Count)   MISSING $($missIds.Count)  ($([math]::Round(100.0*($srcIds.Count-$missIds.Count)/[Math]::Max(1,$srcIds.Count),1))% covered)"
"nums   : source $($srcNums.Count) -> new $($newNums.Count)   MISSING $($missNums.Count)"
""
if ($missIds.Count) { "--- ids only in SOURCE ---"; $missIds | Select-Object -First $MaxList | ForEach-Object { "  [id] $_" } ; "" }
if ($missNums.Count) { "--- nums only in SOURCE ---"; $missNums | Select-Object -First $MaxList | ForEach-Object { "  [num] $_" } }
"=== END ==="
