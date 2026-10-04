param([string[]]$Phases=@('before','after'))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../paths.ps1')
$root=(Get-InkUIPaths $PSScriptRoot).Repository
Push-Location $root
try {
    . './.dsh/uni.ps1'
    foreach($phase in $Phases) {
        Uni 'play:off' 20 | Out-Null
        Uni "inkqa:dynamic$phase" 20 | Out-Null
        Uni 'play:on' 25 | Out-Null
        Uni 'inkqa:seed' 20 | Out-Null
        foreach($resolution in @('1920x1080','1280x720')) {
            Uni "screen:$resolution" 20 | Out-Null
            for($tab=0;$tab -lt 8;$tab++) {
                $name=@('背包','境界','神通','法宝','灵阵','战阵','坐骑','外观')[$tab]
                Uni "inkqa:role:$tab" 20 | Out-Null
                Start-Sleep -Milliseconds 600
                Uni "shot2:screenshots/UI_墨点导航_${phase}_${name}_${resolution}.png" 20 | Out-Null
            }
            if($phase -eq 'after') {
                Uni 'inkqa:close' 20 | Out-Null
                Uni 'inkqa:dynamic:record' 20 | Out-Null
                $deadline=(Get-Date).AddSeconds(60)
                do {
                    Start-Sleep -Milliseconds 1000
                    $result=Uni 'inkqa:dynamic:status' 15
                } while($result.Trim() -eq 'recording' -and (Get-Date) -lt $deadline)
                if($result -notmatch 'DONE Failures=0') { throw "Navigation QA failed: $result" }
                Write-Output $result.Trim()
                Uni 'inkqa:validate' 25 | ForEach-Object {
                    [IO.File]::WriteAllText((Join-Path $root "cultivation/screenshots/InkUI-dynamic-gameplaychecks-$resolution.txt"),$_,[Text.UTF8Encoding]::new($false))
                    if($_ -notmatch 'Failures=0') { throw "Gameplay checks failed: $_" }
                }
            }
            Write-Output "Captured $phase $resolution all eight tabs"
        }
    }
    Uni 'screen:1920x1080' 20 | Out-Null
    Uni 'inkqa:dynamic:parent' 20 | Out-Null
    Start-Sleep -Milliseconds 600
    Uni 'shot2:screenshots/UI_父导航_无底板_1920x1080.png' 20 | Out-Null
    Uni 'screen:1280x720' 20 | Out-Null
    Start-Sleep -Milliseconds 600
    Uni 'shot2:screenshots/UI_父导航_无底板_1280x720.png' 20 | Out-Null
} finally {
    Uni 'play:off' 20 | Out-Null
    Uni 'inkqa:dynamicafter' 20 | Out-Null
    Pop-Location
}
