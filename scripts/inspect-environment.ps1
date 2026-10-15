#Requires -Version 7.0
[CmdletBinding()]
param([string]$DalamudPath)

$ErrorActionPreference = 'Stop'
Write-Output "OS: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)"
Write-Output "Architecture: $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)"
Write-Output "PowerShell: $($PSVersionTable.PSVersion)"
foreach ($tool in @('git', 'dotnet')) {
    if (Get-Command $tool -ErrorAction SilentlyContinue) {
        if ($tool -eq 'dotnet') { & dotnet --list-sdks }
        else { & git --version }
    } else { Write-Output "$tool : not installed / not on PATH" }
}

$candidates = @($DalamudPath, $env:DALAMUD_HOME)
if ($env:APPDATA) {
    $candidates += Join-Path $env:APPDATA 'XIVLauncher/addon/Hooks/dev'
    $candidates += Join-Path $env:APPDATA 'XIVLauncherKR/addon/Hooks/dev'
}
$found = $false
foreach ($candidate in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
    $dll = Join-Path $candidate 'Dalamud.dll'
    if (Test-Path -LiteralPath $dll -PathType Leaf) {
        $found = $true
        $version = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version
        Write-Output "Dalamud: $version at $candidate"
    }
}
if (!$found) { Write-Output 'Dalamud not found in common paths. Re-run with -DalamudPath <directory containing Dalamud.dll>.' }
