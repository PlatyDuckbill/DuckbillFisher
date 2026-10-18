#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$DalamudPath = $env:DALAMUD_HOME,
    [string]$GamePath = $env:FFXIV_GAME_PATH
)

# Dot-source to initialize this shell only. No profile/registry writes or downloads.
$duckbillDevRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$DalamudPath -and $env:APPDATA) {
    $DalamudPath = Join-Path $env:APPDATA 'XIVLauncherKR/addon/Hooks/dev'
}
if (!$DalamudPath -or !(Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll') -PathType Leaf)) {
    throw 'Specify -DalamudPath or DALAMUD_HOME pointing to the inspected directory containing Dalamud.dll.'
}
$duckbillDevRefs = (Resolve-Path -LiteralPath $DalamudPath -ErrorAction Stop).Path
if ($GamePath) {
    if (!(Test-Path -LiteralPath (Join-Path $GamePath 'ffxivgame.ver') -PathType Leaf)) {
        throw 'GamePath must point to the game directory containing ffxivgame.ver.'
    }
    $GamePath = (Resolve-Path -LiteralPath $GamePath -ErrorAction Stop).Path
}
# Commit environment values only after all supplied paths have been validated.
$env:DUCKBILL_ROOT = $duckbillDevRoot
$env:DALAMUD_HOME = $duckbillDevRefs
if ($GamePath) { $env:FFXIV_GAME_PATH = $GamePath }
Write-Output "DUCKBILL_ROOT=$env:DUCKBILL_ROOT"
Write-Output "DALAMUD_HOME=$env:DALAMUD_HOME"
Write-Output 'Build references selected. This does not establish native runtime compatibility.'
