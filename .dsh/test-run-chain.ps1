# .dsh/test-run-chain.ps1 -- full automated check of the act-4 opening chain.
# ASCII only (powershell.exe reads .ps1 as ANSI; non-ASCII breaks parsing).
#
# Steps: refresh -> open Village -> stop -> play -> accept q_main_004 and push to
#        stage 3 -> let the "cong ci..." curtain play and the condition advance it
#        -> poll until scene == Sect and the curtain is hidden.

$ErrorActionPreference = 'Continue'
$root   = Split-Path $PSScriptRoot -Parent
$cowork = Join-Path $env:LOCALAPPDATA 'Programs\Tuanjie Cowork\cli\bin\win32-x64'
$node   = Join-Path $cowork 'node.exe'
if (-not (Test-Path $node)) { $node = 'node' }
$mcp      = Join-Path $PSScriptRoot 'mcp-call.mjs'
$argsFile = Join-Path $env:TEMP 'mcp-args.json'
$enc      = New-Object System.Text.UTF8Encoding($false)

function Mcp([string]$tool, [hashtable]$obj) {
    [System.IO.File]::WriteAllText($argsFile, ($obj | ConvertTo-Json -Depth 8 -Compress), $enc)
    $o = & $node $mcp call $tool "@$argsFile" 2>&1 | Out-String
    try { return ($o | ConvertFrom-Json) } catch { return [pscustomobject]@{ raw = $o } }
}
function ReadCs([string]$n) { return [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot $n), [System.Text.Encoding]::UTF8) }

Mcp 'unity_editor' @{ action = 'stop' } | Out-Null
Mcp 'exec_editor_script' @{ script = 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Village.scene"); return "ok";'; summary = 'open Village' } | Out-Null
Mcp 'unity_editor' @{ action = 'play'; timeoutSeconds = 60 } | Out-Null
Start-Sleep -Seconds 8

$push = ReadCs 'probe-push.cs'
$r = Mcp 'exec_runtime_script' @{ script = $push; summary = 'push to stage3' }
Write-Host "  push: $($r.result)"

$probe = ReadCs 'probe-state.cs'
for ($i = 1; $i -le 40; $i++) {
    Start-Sleep -Seconds 2
    $p = Mcp 'exec_runtime_script' @{ script = $probe; summary = 'snap' }
    Write-Host ("  [{0,2}] {1}" -f $i, $p.result)
    if ("$($p.result)" -match 'scene=Sect' -or "$($p.result)" -match ([char]0x573A+[char]0x666F+'=Sect')) {
        Write-Host '  ===> reached Sect'
        break
    }
}

# 额外确认幕是否真的关了
$hide = ReadCs 'probe-state.cs'
$h = Mcp 'exec_runtime_script' @{ script = $hide; summary = 'curtains' }
Write-Host "  curtain: $($h.result)"
