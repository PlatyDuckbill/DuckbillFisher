#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DalamudPath,
    [string]$GamePath,
    [string]$OutputPath
)

# Read installed assembly metadata only. Never load game assemblies for execution,
# resolve game addresses, attach to a process, or call native methods.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$referencePath = (Resolve-Path -LiteralPath $DalamudPath).Path
Add-Type -Path (Join-Path $referencePath 'Mono.Cecil.dll')

$typeNames = @(
    'Dalamud.Plugin.Services.IPlayerState',
    'Dalamud.Game.Player.PlayerState',
    'FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState',
    'Lumina.Excel.Sheets.FishParameter',
    'Lumina.Excel.RowRef',
    'InteropGenerator.Runtime.BitArray'
)
$methodNames = @(
    'IsFishCaught', 'Instance', 'get_IsLoaded', 'get_CaughtFishBitArray',
    'get_Item', 'get_IsInLog', 'Get'
)
$assemblies = foreach ($file in @('Dalamud.dll', 'FFXIVClientStructs.dll', 'InteropGenerator.Runtime.dll', 'Lumina.Excel.dll', 'Lumina.dll')) {
    $path = Join-Path $referencePath $file
    $options = [Mono.Cecil.ReaderParameters]::new()
    $options.ReadSymbols = Test-Path -LiteralPath ([IO.Path]::ChangeExtension($path, '.pdb'))
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($path, $options)
    try {
        $metadata = [ordered]@{}
        $informationalVersion = $null
        foreach ($attribute in $module.Assembly.CustomAttributes) {
            if ($attribute.AttributeType.Name -eq 'AssemblyInformationalVersionAttribute') {
                $informationalVersion = [string]$attribute.ConstructorArguments[0].Value
            }
            if ($attribute.AttributeType.Name -eq 'AssemblyMetadataAttribute') {
                $metadata[[string]$attribute.ConstructorArguments[0].Value] = [string]$attribute.ConstructorArguments[1].Value
            }
        }
        $sourceLinks = @(foreach ($info in $module.CustomDebugInformations) {
            if ($info -is [Mono.Cecil.Cil.SourceLinkDebugInformation]) {
                $documents = ($info.Content | ConvertFrom-Json -AsHashtable).documents
                foreach ($entry in $documents.GetEnumerator()) {
                    if ($entry.Value -match '/(Dalamud|FFXIVClientStructs|Lumina|Lumina.Excel|EXDSchema)/') {
                        [string]$entry.Value
                    }
                }
            }
        })
        $candidates = @(foreach ($type in $module.Types | Where-Object { $_.Namespace -eq 'Dalamud.Plugin.Services' -and $_.IsInterface -and $_.IsPublic }) {
            foreach ($method in $type.Methods | Where-Object { $_.Name -match 'Fish|Caught' }) {
                [string]$method.FullName
            }
        })
        $types = @(foreach ($type in $module.Types | Where-Object { $_.FullName -in $typeNames }) {
            [ordered]@{
                name = $type.FullName
                size = $type.ClassSize
                fields = @(foreach ($field in $type.Fields | Where-Object { $_.Name -match 'caughtFish|IsLoaded|NumFishCaught' }) {
                    [ordered]@{ name = $field.FullName; offset = $field.Offset }
                })
                properties = @($type.Properties | ForEach-Object { [string]$_.FullName })
                methods = @(foreach ($method in $type.Methods | Where-Object { $_.Name -in $methodNames }) {
                    [ordered]@{
                        name = $method.FullName
                        parameters = @($method.Parameters | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" })
                        attributes = @(foreach ($attribute in $method.CustomAttributes) {
                            [ordered]@{
                                name = $attribute.AttributeType.FullName
                                arguments = @($attribute.ConstructorArguments | ForEach-Object { [string]$_.Value })
                            }
                        })
                        il = @(if ($method.HasBody) { $method.Body.Instructions | ForEach-Object { $_.ToString() } })
                    }
                })
            }
        })
        [ordered]@{
            file = $file
            assembly = $module.Assembly.Name.FullName
            moduleId = $module.Mvid.ToString()
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            informationalVersion = $informationalVersion
            metadata = $metadata
            symbolsRead = $options.ReadSymbols
            sourceLinks = $sourceLinks
            publicFishServiceCandidates = $candidates
            types = $types
        }
    } finally { $module.Dispose() }
}

$gameVersion = $null
if ($GamePath) {
    $versionFile = Join-Path (Resolve-Path -LiteralPath $GamePath).Path 'game/ffxivgame.ver'
    $gameVersion = (Get-Content -Raw -LiteralPath $versionFile).Trim()
}
$report = [ordered]@{
    format = 'ENV-03/fish-api-metadata/v1'
    inspectedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    scope = 'Installed file metadata only; not an in-game compatibility or collection-state result.'
    gameVersion = $gameVersion
    assemblies = @($assemblies)
}
$json = $report | ConvertTo-Json -Depth 15
if ($OutputPath) {
    $json | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Output "Fish API metadata report: $([IO.Path]::GetFullPath($OutputPath))"
} else {
    Write-Output $json
}
