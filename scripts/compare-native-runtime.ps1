#Requires -Version 7.0
# Offline metadata inspection only. Never enables a runtime profile or executes game code.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselinePath,
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$baseline = (Resolve-Path -LiteralPath $BaselinePath).Path
$candidate = (Resolve-Path -LiteralPath $CandidatePath).Path
Add-Type -Path (Join-Path $baseline 'Mono.Cecil.dll')

function Get-Attributes($value) {
    @($value.CustomAttributes | ForEach-Object {
        $_.AttributeType.FullName + ':' + [Convert]::ToHexString($_.GetBlob())
    })
}

function Get-TypeDescription($type) {
    [ordered]@{
        Name = $type.FullName; Attributes = [string]$type.Attributes
        Base = [string]$type.BaseType; Size = $type.ClassSize; Packing = $type.PackingSize
        AttributesData = @(Get-Attributes $type)
        Interfaces = @($type.Interfaces | ForEach-Object { $_.InterfaceType.FullName })
        Fields = @($type.Fields | ForEach-Object {
            [ordered]@{
                Name = $_.Name; Type = $_.FieldType.FullName; Offset = $_.Offset
                Attributes = [string]$_.Attributes; AttributesData = @(Get-Attributes $_)
                Constant = if ($_.HasConstant) { [string]$_.Constant } else { $null }
                Data = [Convert]::ToHexString($_.InitialValue)
            }
        })
        Methods = @($type.Methods | ForEach-Object {
            [ordered]@{
                Signature = $_.FullName; Attributes = [string]$_.Attributes
                ImplAttributes = [string]$_.ImplAttributes; AttributesData = @(Get-Attributes $_)
                ReturnAttributes = @(Get-Attributes $_.MethodReturnType)
                Parameters = @($_.Parameters | ForEach-Object {
                    [ordered]@{
                        Name = $_.Name; Type = $_.ParameterType.FullName; Attributes = [string]$_.Attributes
                        AttributesData = @(Get-Attributes $_)
                        Constant = if ($_.HasConstant) { [string]$_.Constant } else { $null }
                    }
                })
                Instructions = if ($_.HasBody) { @($_.Body.Instructions | ForEach-Object ToString) } else { @() }
                Locals = if ($_.HasBody) { @($_.Body.Variables | ForEach-Object { $_.VariableType.FullName }) } else { @() }
                Handlers = if ($_.HasBody) { @($_.Body.ExceptionHandlers | ForEach-Object {
                    [ordered]@{
                        Kind = [string]$_.HandlerType; Catch = [string]$_.CatchType
                        TryStart = [string]$_.TryStart; TryEnd = [string]$_.TryEnd
                        HandlerStart = [string]$_.HandlerStart; HandlerEnd = [string]$_.HandlerEnd
                        FilterStart = [string]$_.FilterStart
                    }
                }) } else { @() }
            }
        })
        Nested = @($type.NestedTypes | ForEach-Object { Get-TypeDescription $_ })
    }
}

# Review this scope when native consumers change. Equality is evidence for these types,
# not proof that every transitive dependency or the running game is compatible.
$groups = [ordered]@{
    'Dalamud.dll' = @(
        'Dalamud.Plugin.Services.IPlayerState', 'Dalamud.Game.Player.PlayerState',
        'Dalamud.Plugin.Services.IGameGui', 'Dalamud.Game.Gui.GameGui',
        'Dalamud.Plugin.Services.IChatGui', 'Dalamud.Game.Gui.ChatGui',
        'Dalamud.Game.Chat.ILogMessage', 'Dalamud.Game.Chat.LogMessage',
        'Dalamud.Game.Chat.LogMessageParameterList', 'Dalamud.Game.Chat.ILogMessageEntity',
        'Dalamud.Game.Chat.LogMessageEntity', 'Dalamud.Game.ChatHandlers'
    )
    'FFXIVClientStructs.dll' = @(
        'FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState',
        'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentFishGuide',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandlerModule',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.FishingEventHandler',
        'FFXIVClientStructs.FFXIV.Client.UI.UIGlobals',
        'FFXIVClientStructs.FFXIV.Client.UI.UIModule',
        'FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkModule',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkStage',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipType',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkUldManager',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNode',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentBase',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentButton',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentRadioButton',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkTextNode',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkValue',
        'FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType',
        'FFXIVClientStructs.FFXIV.Component.GUI.NodeType',
        'FFXIVClientStructs.FFXIV.Component.GUI.NodeFlags',
        'FFXIVClientStructs.FFXIV.Client.Enums.DetailKind',
        'FFXIVClientStructs.FFXIV.Client.Graphics.ByteColor',
        'FFXIVClientStructs.Interop.Pointer`1',
        'FFXIVClientStructs.STD.StdVector`1', 'FFXIVClientStructs.STD.StdVector`2',
        'FFXIVClientStructs.STD.StdMap`2', 'FFXIVClientStructs.STD.StdMap`3'
    )
    'InteropGenerator.Runtime.dll' = @('InteropGenerator.Runtime.BitArray')
}

$modules = @()
$comparisons = @()
foreach ($dll in $groups.Keys) {
    $old = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $baseline $dll))
    $new = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $candidate $dll))
    try {
        $modules += [ordered]@{
            Module = $dll
            Baseline = [ordered]@{
                Version = $old.Assembly.Name.Version.ToString(); Mvid = $old.Mvid.ToString()
                Sha256 = (Get-FileHash -LiteralPath (Join-Path $baseline $dll) -Algorithm SHA256).Hash
            }
            Candidate = [ordered]@{
                Version = $new.Assembly.Name.Version.ToString(); Mvid = $new.Mvid.ToString()
                Sha256 = (Get-FileHash -LiteralPath (Join-Path $candidate $dll) -Algorithm SHA256).Hash
            }
        }
        foreach ($name in $groups[$dll]) {
            $oldType = $old.GetType($name)
            $newType = $new.GetType($name)
            if ($null -eq $oldType -or $null -eq $newType) { throw "Missing inspection type: $dll / $name" }
            $before = Get-TypeDescription $oldType
            $after = Get-TypeDescription $newType
            $comparisons += [ordered]@{
                Module = $dll; Type = $name
                Equal = (($before | ConvertTo-Json -Depth 60 -Compress) -ceq ($after | ConvertTo-Json -Depth 60 -Compress))
                Baseline = $before; Candidate = $after
            }
        }
    } finally { $old.Dispose(); $new.Dispose() }
}
$report = [ordered]@{ Format = 'native-metadata/v1'; Modules = $modules; Comparisons = $comparisons }
$report | ConvertTo-Json -Depth 65 | Set-Content -LiteralPath $ReportPath -Encoding utf8
$different = @($comparisons | Where-Object { -not $_.Equal })
Write-Output "Inspected=$($comparisons.Count); Equal=$($comparisons.Count - $different.Count); Different=$($different.Count)"
$different | ForEach-Object { Write-Output "Different: $($_.Module) / $($_.Type)" }
if ($different.Count -gt 0) { throw 'Metadata differences require review. No runtime profile has been changed.' }
