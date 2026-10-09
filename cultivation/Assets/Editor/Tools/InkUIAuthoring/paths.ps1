function Get-InkUIPaths([string]$ToolDirectory) {
    $toolkit = $PSScriptRoot
    $project = [IO.Path]::GetFullPath((Join-Path $toolkit '../../../..'))
    $authoring = Join-Path $project 'Assets/Art/UI/Source/InkUI/Authoring'
    $relative = $ToolDirectory.Substring($toolkit.Length).TrimStart('\','/')
    [pscustomobject]@{
        Repository = Split-Path -Parent $project
        Authoring = Join-Path $authoring $relative
        Runtime = Join-Path $project 'Assets/resources/UI/InkUI'
    }
}
