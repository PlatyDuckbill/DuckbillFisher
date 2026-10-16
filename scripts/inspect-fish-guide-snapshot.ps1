#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DalamudPath,
    [string]$OutputPath
)

# Installed file metadata only: no game process, native calls, address resolution, or network.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$referencePath = (Resolve-Path -LiteralPath $DalamudPath).Path
Add-Type -Path (Join-Path $referencePath 'Mono.Cecil.dll')
$structPath = Join-Path $referencePath 'FFXIVClientStructs.dll'
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($structPath)
try {
    $agent = $module.Types | Where-Object FullName -EQ 'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentFishGuide'
    $data = $agent.NestedTypes | Where-Object Name -EQ 'FishGuideDataBase'
    $entry = $data.NestedTypes | Where-Object Name -EQ 'FishGuideListEntry'
    $specifications = @(
        @{ Type = $agent; Size = 0x1A8; Fields = @{ Mode = 0x31; Data = 0x48 } },
        @{ Type = $data; Size = 0x458; Fields = @{ Agent = 0x08; Entries = 0x10; IsFilteredEntriesLoaded = 0x382 } },
        @{ Type = $entry; Size = 0x14; Fields = @{ ItemId = 0x00; Id = 0x04; IsCaught = 0x0E } }
    )
    $layouts = @(foreach ($specification in $specifications) {
        $type = $specification.Type
        if ($null -eq $type -or $type.ClassSize -ne $specification.Size) {
            throw 'Installed guide type layout differs from the inspected revision.'
        }
        $fields = @(foreach ($name in $specification.Fields.Keys | Sort-Object) {
            $field = $type.Fields | Where-Object Name -EQ $name
            if ($null -eq $field -or $field.Offset -ne $specification.Fields[$name]) {
                throw "Installed guide field differs: $($type.FullName)::$name"
            }
            [ordered]@{ name = $name; offset = $field.Offset; type = $field.FieldType.FullName }
        })
        [ordered]@{ type = $type.FullName; size = $type.ClassSize; fields = $fields }
    })
    $report = [ordered]@{
        format = 'MVP-04/guide-snapshot-metadata/v1'
        scope = 'Installed metadata only; not an in-game result or proof of network receipt.'
        sourceRevision = 'cae2c3755ef02f9f2728082ae8043669ac9fbfe3'
        moduleId = $module.Mvid.ToString()
        sha256 = (Get-FileHash -LiteralPath $structPath -Algorithm SHA256).Hash
        layouts = $layouts
    }
    if ($OutputPath) {
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    }
    Write-Output "Guide snapshot metadata: $($layouts.Count) layouts matched; no game memory read."
} finally { $module.Dispose() }
