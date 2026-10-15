#Requires -Version 7.0
[CmdletBinding()]
param(
    [switch]$CoreOnly,
    [string]$DalamudPath = $env:DALAMUD_HOME,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$DeployDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit $LASTEXITCODE). No deployment performed." }
}

if ($CoreOnly -and $DeployDirectory) { throw '-CoreOnly cannot be combined with -DeployDirectory.' }
if ($DeployDirectory) {
    $DeployDirectory = [IO.Path]::GetFullPath($DeployDirectory)
    if (!(Split-Path $DeployDirectory -Leaf).Equals('DuckbillFisher', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'DeployDirectory must be a dedicated directory named DuckbillFisher.'
    }
}
if ($DalamudPath) { $DalamudPath = (Resolve-Path -LiteralPath $DalamudPath).Path }

Push-Location $root
try {
    $requiredSdk = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    $actualSdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $actualSdk -ne $requiredSdk) {
        throw "Install .NET SDK $requiredSdk (see docs/WINDOWS-SETUP.md)."
    }
    $tests = 'tests/DuckbillFisher.Core.Tests/DuckbillFisher.Core.Tests.csproj'
    Invoke-Dotnet restore $tests --locked-mode
    Invoke-Dotnet test $tests --no-restore -c $Configuration
    if ($CoreOnly) { return }

    if (!$DalamudPath) { throw 'Specify -DalamudPath or DALAMUD_HOME. Run scripts/inspect-environment.ps1 to find candidates.' }
    $lib = (Resolve-Path -LiteralPath $DalamudPath).Path
    $dalamudDll = Join-Path $lib 'Dalamud.dll'
    $version = [Reflection.AssemblyName]::GetAssemblyName($dalamudDll).Version
    if ($version.Major -ne 15) { throw "Expected Dalamud 15, found $version. Review SDK compatibility before upgrading." }
    Write-Output "Building against Dalamud $version at $lib"

    $plugin = 'src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj'
    Invoke-Dotnet restore $plugin --locked-mode "-p:DalamudLibPath=$lib/"
    Invoke-Dotnet build $plugin --no-restore -c $Configuration "-p:DalamudLibPath=$lib/"
    $output = Join-Path $root "src/DuckbillFisher.Plugin/bin/$Configuration"
    $manifest = Get-Content (Join-Path $output 'DuckbillFisher.json') -Raw | ConvertFrom-Json
    if ($manifest.InternalName -ne 'DuckbillFisher' -or $manifest.DalamudApiLevel -ne 15) {
        throw 'Unexpected generated manifest; deployment cancelled.'
    }

    # Only our assemblies and manifest. Never copy the Dalamud runtime or game files.
    $files = @('DuckbillFisher.dll', 'DuckbillFisher.Core.dll', 'DuckbillFisher.json')
    foreach ($file in $files) {
        if (!(Test-Path -LiteralPath (Join-Path $output $file) -PathType Leaf)) { throw "Missing output: $file" }
    }
    if ($DeployDirectory) {
        $destination = [IO.Path]::GetFullPath($DeployDirectory)
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $output $file) -Destination (Join-Path $destination $file) -Force }
        Write-Output "Copied to $destination. Register DuckbillFisher.dll in Dev Plugin Locations. Unload the plugin before future copies."
    } else {
        Write-Output "Dev plugin DLL: $(Join-Path $output 'DuckbillFisher.dll')"
    }
} finally { Pop-Location }
