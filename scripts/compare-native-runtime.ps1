#Requires -Version 7.0
# Offline metadata inspection only. Never changes runtime behavior or executes game code.
# Differences in members that the plugin uses (directly or through the compared types'
# own IL) fail the check. Differences in other members are reported as warnings.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselinePath,
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$PluginPath = 'src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll'
)

$ErrorActionPreference = 'Stop'
$baseline = (Resolve-Path -LiteralPath $BaselinePath).Path
$candidate = (Resolve-Path -LiteralPath $CandidatePath).Path
if (-not (Test-Path -LiteralPath $PluginPath -PathType Leaf)) {
    throw "Plugin DLL not found: $PluginPath. Build the plugin first or pass -PluginPath."
}
$plugin = (Resolve-Path -LiteralPath $PluginPath).Path
Add-Type -Path (Join-Path $baseline 'Mono.Cecil.dll')

function Get-Attributes($value) {
    @($value.CustomAttributes | ForEach-Object {
        $_.AttributeType.FullName + ':' + [Convert]::ToHexString($_.GetBlob())
    })
}

function Get-TypeHeader($type) {
    [ordered]@{
        Name = $type.FullName; Attributes = [string]$type.Attributes
        Base = [string]$type.BaseType; Size = $type.ClassSize; Packing = $type.PackingSize
        AttributesData = @(Get-Attributes $type)
        Interfaces = @($type.Interfaces | ForEach-Object { $_.InterfaceType.FullName })
    }
}

function Get-FieldDescription($field) {
    [ordered]@{
        Name = $field.Name; Type = $field.FieldType.FullName; Offset = $field.Offset
        Attributes = [string]$field.Attributes; AttributesData = @(Get-Attributes $field)
        Constant = if ($field.HasConstant) { [string]$field.Constant } else { $null }
        Data = [Convert]::ToHexString($field.InitialValue)
    }
}

function Get-MethodDescription($method) {
    [ordered]@{
        Signature = $method.FullName; Attributes = [string]$method.Attributes
        ImplAttributes = [string]$method.ImplAttributes; AttributesData = @(Get-Attributes $method)
        ReturnAttributes = @(Get-Attributes $method.MethodReturnType)
        Parameters = @($method.Parameters | ForEach-Object {
            [ordered]@{
                Name = $_.Name; Type = $_.ParameterType.FullName; Attributes = [string]$_.Attributes
                AttributesData = @(Get-Attributes $_)
                Constant = if ($_.HasConstant) { [string]$_.Constant } else { $null }
            }
        })
        Instructions = if ($method.HasBody) { @($method.Body.Instructions | ForEach-Object ToString) } else { @() }
        Locals = if ($method.HasBody) { @($method.Body.Variables | ForEach-Object { $_.VariableType.FullName }) } else { @() }
        Handlers = if ($method.HasBody) { @($method.Body.ExceptionHandlers | ForEach-Object {
            [ordered]@{
                Kind = [string]$_.HandlerType; Catch = [string]$_.CatchType
                TryStart = [string]$_.TryStart; TryEnd = [string]$_.TryEnd
                HandlerStart = [string]$_.HandlerStart; HandlerEnd = [string]$_.HandlerEnd
                FilterStart = [string]$_.FilterStart
            }
        }) } else { @() }
    }
}

function Get-FieldKey($field) { $field.DeclaringType.FullName + '::' + $field.Name }
function Get-MethodKey($method) { $method.FullName }

function Get-AllTypes($type) {
    $type
    foreach ($nested in $type.NestedTypes) { Get-AllTypes $nested }
}

function Get-InstructionReferences($method) {
    if (-not $method.HasBody) { return }
    foreach ($instruction in $method.Body.Instructions) {
        $operand = $instruction.Operand
        if ($operand -is [Mono.Cecil.MethodReference] -or $operand -is [Mono.Cecil.FieldReference]) { $operand }
    }
}

function Resolve-Member($reference) {
    try { $reference.Resolve() } catch { $null }
}

function Get-ParameterText($method) { (@($method.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') }

# Interface method key -> implementing method keys inside the compared types. The plugin calls
# Dalamud services through interfaces, so the implementation counts as used too.
function Get-ImplementationMap($definitions) {
    $map = @{}
    $types = @($definitions.Values | ForEach-Object { $_.DeclaringType } | Sort-Object -Property FullName -Unique)
    foreach ($type in $types) {
        if ($type.IsInterface) { continue }
        foreach ($implemented in $type.Interfaces) {
            $interface = Resolve-Member $implemented.InterfaceType
            if ($null -eq $interface) { continue }
            foreach ($interfaceMethod in $interface.Methods) {
                $parameters = Get-ParameterText $interfaceMethod
                $explicitName = $interface.FullName + '.' + $interfaceMethod.Name
                foreach ($method in $type.Methods) {
                    $explicit = @($method.Overrides | Where-Object { (Get-MethodKey (Resolve-Member $_)) -eq (Get-MethodKey $interfaceMethod) }).Count -gt 0
                    $implicit = ($method.Name -eq $interfaceMethod.Name -or $method.Name -eq $explicitName) -and
                        (Get-ParameterText $method) -eq $parameters
                    if (-not ($explicit -or $implicit)) { continue }
                    $key = Get-MethodKey $interfaceMethod
                    if (-not $map.ContainsKey($key)) { $map[$key] = [System.Collections.Generic.List[string]]::new() }
                    $map[$key].Add((Get-MethodKey $method))
                }
            }
        }
    }
    $map
}

# Members of the compared types that are reachable from the plugin's IL, following calls
# inside the compared types. Keys are stable across modules (full names).
function Get-UsedKeys($pluginModule, $definitions) {
    $used = [System.Collections.Generic.HashSet[string]]::new()
    $queue = [System.Collections.Generic.Queue[object]]::new()
    $implementations = Get-ImplementationMap $definitions
    $mark = {
        param($key)
        if (-not $definitions.ContainsKey($key)) { return }
        if (-not $used.Add($key)) { return }
        $definition = $definitions[$key]
        if ($definition -is [Mono.Cecil.MethodDefinition]) {
            $queue.Enqueue($definition)
            if ($implementations.ContainsKey($key)) { foreach ($implementation in $implementations[$key]) { & $mark $implementation } }
        }
    }
    $visit = {
        param($reference)
        $definition = Resolve-Member $reference
        if ($null -eq $definition) { return }
        $key = if ($definition -is [Mono.Cecil.FieldDefinition]) { Get-FieldKey $definition } else { Get-MethodKey $definition }
        & $mark $key
    }
    foreach ($type in $pluginModule.Types) {
        foreach ($inner in Get-AllTypes $type) {
            foreach ($method in $inner.Methods) {
                foreach ($reference in Get-InstructionReferences $method) { & $visit $reference }
            }
        }
    }
    while ($queue.Count -gt 0) {
        foreach ($reference in Get-InstructionReferences $queue.Dequeue()) { & $visit $reference }
    }
    ,$used
}

function Get-Uncovered($pluginModule, $groups) {
    $listed = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($dll in $groups.Keys) {
        foreach ($name in $groups[$dll]) { [void]$listed.Add($name) }
    }
    $seen = [System.Collections.Generic.SortedSet[string]]::new()
    foreach ($reference in $pluginModule.GetMemberReferences()) {
        $declaring = $reference.DeclaringType
        if ($null -eq $declaring) { continue }
        $scope = $declaring.Scope.Name
        if ($scope -ne 'FFXIVClientStructs' -and $scope -ne 'InteropGenerator.Runtime') { continue }
        $root = $declaring.GetElementType()
        while ($null -ne $root.DeclaringType) { $root = $root.DeclaringType }
        if (-not $listed.Contains($root.FullName)) { [void]$seen.Add("$scope / $($root.FullName)") }
    }
    @($seen)
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
        'Dalamud.Game.Chat.LogMessageEntity', 'Dalamud.Game.ChatHandlers',
        'Dalamud.Plugin.Services.IContextMenu', 'Dalamud.Game.Gui.ContextMenu.IMenuArgs',
        'Dalamud.Game.Gui.ContextMenu.IMenuOpenedArgs', 'Dalamud.Game.Gui.ContextMenu.MenuItem',
        'Dalamud.Game.Gui.ContextMenu.MenuTargetInventory', 'Dalamud.Game.Inventory.GameInventoryItem',
        'Dalamud.Plugin.Services.IGameInventory', 'Dalamud.Game.Inventory.GameInventory',
        'Dalamud.Game.Inventory.GameInventoryPluginScoped', 'Dalamud.Game.Inventory.GameInventoryType',
        'Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryEventArgs',
        'Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemAddedArgs',
        'Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemRemovedArgs',
        'Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemChangedArgs',
        'Dalamud.Game.Gui.ContextMenu.MenuTargetDefault', 'Dalamud.Utility.ItemUtil',
        'Dalamud.Game.ClientState.Statuses.Status', 'Dalamud.Game.ClientState.Statuses.StatusList',
        'Dalamud.Game.ClientState.Objects.Types.BattleChara'
    )
    'FFXIVClientStructs.dll' = @(
        'FFXIVClientStructs.FFXIV.Client.System.Framework.Framework',
        'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule',
        'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInterface',
        'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentChatLog',
        'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId',
        'FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState',
        'FFXIVClientStructs.FFXIV.Client.Game.Status',
        'FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentFishGuide',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework',
        'FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentOceanFishing',
        'FFXIVClientStructs.FFXIV.Client.Game.WeatherManager',
        'FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentDirector',
        'FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.ContentDirector',
        'FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentType',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandlerModule',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.FishingEventHandler',
        'FFXIVClientStructs.FFXIV.Client.Game.Event.FishingState',
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
        'FFXIVClientStructs.FFXIV.Common.Math.Matrix2x2',
        'FFXIVClientStructs.Interop.Pointer`1',
        'FFXIVClientStructs.STD.StdVector`1', 'FFXIVClientStructs.STD.StdVector`2',
        'FFXIVClientStructs.STD.StdMap`2', 'FFXIVClientStructs.STD.StdMap`3'
    )
    'InteropGenerator.Runtime.dll' = @('InteropGenerator.Runtime.BitArray')
}

function Read-Module($directory, $dll) {
    $resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    $resolver.AddSearchDirectory($directory)
    $parameters = [Mono.Cecil.ReaderParameters]::new()
    $parameters.AssemblyResolver = $resolver
    [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $directory $dll), $parameters)
}

function Read-Plugin($directory) {
    $resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
    $resolver.AddSearchDirectory($directory)
    $resolver.AddSearchDirectory((Split-Path -Parent $plugin))
    $parameters = [Mono.Cecil.ReaderParameters]::new()
    $parameters.AssemblyResolver = $resolver
    [Mono.Cecil.ModuleDefinition]::ReadModule($plugin, $parameters)
}

function Get-Definitions($modules) {
    $definitions = @{}
    foreach ($dll in $groups.Keys) {
        foreach ($name in $groups[$dll]) {
            $type = $modules[$dll].GetType($name)
            if ($null -eq $type) { continue }
            foreach ($inner in Get-AllTypes $type) {
                foreach ($field in $inner.Fields) { $definitions[(Get-FieldKey $field)] = $field }
                foreach ($method in $inner.Methods) { $definitions[(Get-MethodKey $method)] = $method }
            }
        }
    }
    $definitions
}

function ConvertTo-Text($value) { $value | ConvertTo-Json -Depth 60 -Compress }

$opened = @()
try {
    $oldModules = @{}; $newModules = @{}
    foreach ($dll in $groups.Keys) {
        $oldModules[$dll] = Read-Module $baseline $dll; $opened += $oldModules[$dll]
        $newModules[$dll] = Read-Module $candidate $dll; $opened += $newModules[$dll]
    }
    $oldPlugin = Read-Plugin $baseline; $opened += $oldPlugin
    $newPlugin = Read-Plugin $candidate; $opened += $newPlugin

    $oldDefinitions = Get-Definitions $oldModules
    $newDefinitions = Get-Definitions $newModules
    $used = Get-UsedKeys $oldPlugin $oldDefinitions
    $used.UnionWith((Get-UsedKeys $newPlugin $newDefinitions))
    $uncovered = Get-Uncovered $oldPlugin $groups

    $modules = @()
    foreach ($dll in $groups.Keys) {
        $modules += [ordered]@{
            Module = $dll
            Baseline = [ordered]@{
                Version = $oldModules[$dll].Assembly.Name.Version.ToString(); Mvid = $oldModules[$dll].Mvid.ToString()
                Sha256 = (Get-FileHash -LiteralPath (Join-Path $baseline $dll) -Algorithm SHA256).Hash
            }
            Candidate = [ordered]@{
                Version = $newModules[$dll].Assembly.Name.Version.ToString(); Mvid = $newModules[$dll].Mvid.ToString()
                Sha256 = (Get-FileHash -LiteralPath (Join-Path $candidate $dll) -Algorithm SHA256).Hash
            }
        }
    }

    $comparisons = @()
    foreach ($dll in $groups.Keys) {
        foreach ($name in $groups[$dll]) {
            $oldType = $oldModules[$dll].GetType($name)
            $newType = $newModules[$dll].GetType($name)
            if ($null -eq $oldType -or $null -eq $newType) { throw "Missing inspection type: $dll / $name" }
            # Breaking: a used member is missing on one side (signature changes show up this way
            # because the key includes the signature). Review: a used member or a listed type's
            # header changed; the plugin binds by name at load time and follows the running
            # ClientStructs, so this is not a failure, but its meaning may have changed.
            # Warning: differences in members the plugin does not reach.
            $breaking = @(); $review = @(); $warnings = @()
            $oldTypes = @{}; foreach ($t in Get-AllTypes $oldType) { $oldTypes[$t.FullName] = $t }
            $newTypes = @{}; foreach ($t in Get-AllTypes $newType) { $newTypes[$t.FullName] = $t }
            foreach ($typeName in @($oldTypes.Keys + $newTypes.Keys | Sort-Object -Unique)) {
                $before = $oldTypes[$typeName]; $after = $newTypes[$typeName]
                $prefix = $typeName + '::'
                $reached = @($used | Where-Object {
                    $_.StartsWith($prefix, [StringComparison]::Ordinal) -or $_.Contains(' ' + $prefix, [StringComparison]::Ordinal)
                }).Count -gt 0
                if ($null -eq $before -or $null -eq $after) {
                    $line = "Type only in $(if ($null -eq $before) { 'candidate' } else { 'baseline' }): $typeName"
                    if ($reached) { $breaking += $line } else { $warnings += $line }
                    continue
                }
                $oldHeader = Get-TypeHeader $before; $newHeader = Get-TypeHeader $after
                if ((ConvertTo-Text $oldHeader) -cne (ConvertTo-Text $newHeader)) {
                    $changed = @($oldHeader.Keys | Where-Object { (ConvertTo-Text $oldHeader[$_]) -cne (ConvertTo-Text $newHeader[$_]) })
                    $detail = ($changed | ForEach-Object {
                        if ($_ -eq 'Size') { "Size $($oldHeader.Size)->$($newHeader.Size)" } else { $_ }
                    }) -join ', '
                    $line = "Type header ($detail): $typeName"
                    if ($typeName -eq $name -or $reached) { $review += $line } else { $warnings += $line }
                }
                $members = @{}
                foreach ($f in $before.Fields) { $members[(Get-FieldKey $f)] = @{ Old = (ConvertTo-Text (Get-FieldDescription $f)) } }
                foreach ($f in $after.Fields) {
                    $key = Get-FieldKey $f
                    if (-not $members.ContainsKey($key)) { $members[$key] = @{} }
                    $members[$key].New = ConvertTo-Text (Get-FieldDescription $f)
                }
                foreach ($m in $before.Methods) { $members[(Get-MethodKey $m)] = @{ Old = (ConvertTo-Text (Get-MethodDescription $m)) } }
                foreach ($m in $after.Methods) {
                    $key = Get-MethodKey $m
                    if (-not $members.ContainsKey($key)) { $members[$key] = @{} }
                    $members[$key].New = ConvertTo-Text (Get-MethodDescription $m)
                }
                foreach ($key in @($members.Keys | Sort-Object)) {
                    $entry = $members[$key]
                    if ($entry.Old -ceq $entry.New) { continue }
                    $isUsed = $used.Contains($key)
                    if ($null -eq $entry.Old -or $null -eq $entry.New) {
                        $side = if ($null -eq $entry.Old) { 'candidate' } else { 'baseline' }
                        $line = "Only in $side`: $key"
                        if ($isUsed) { $breaking += $line } else { $warnings += $line }
                    } elseif ($isUsed) { $review += "Changed: $key" } else { $warnings += "Changed: $key" }
                }
            }
            $comparisons += [ordered]@{
                Module = $dll; Type = $name
                Equal = ($breaking.Count -eq 0 -and $review.Count -eq 0 -and $warnings.Count -eq 0)
                Breaking = $breaking; Review = $review; Warnings = $warnings
            }
        }
    }
} finally {
    foreach ($module in $opened) { $module.Dispose() }
}

$usedList = @($used | Sort-Object)
$report = [ordered]@{
    Format = 'native-metadata/v2'
    Plugin = [ordered]@{ Path = $PluginPath; Sha256 = (Get-FileHash -LiteralPath $plugin -Algorithm SHA256).Hash }
    Modules = $modules
    UsedMembers = $usedList
    UncoveredTypes = $uncovered
    Comparisons = $comparisons
}
$report | ConvertTo-Json -Depth 65 | Set-Content -LiteralPath $ReportPath -Encoding utf8

$breakingCount = @($comparisons | ForEach-Object { $_.Breaking }).Count
$reviewCount = @($comparisons | ForEach-Object { $_.Review }).Count
$warningCount = @($comparisons | ForEach-Object { $_.Warnings }).Count
Write-Output ("Inspected={0}; EqualTypes={1}; Breaking={2}; Review={3}; Warnings={4}; UsedMembers={5}; UncoveredTypes={6}" -f
    $comparisons.Count, @($comparisons | Where-Object Equal).Count, $breakingCount, $reviewCount, $warningCount,
    $usedList.Count, $uncovered.Count)
foreach ($comparison in $comparisons) {
    if ($comparison.Warnings.Count -gt 0) {
        Write-Output "Warning: $($comparison.Module) / $($comparison.Type) ($($comparison.Warnings.Count) differences in members the plugin does not use)"
    }
}
foreach ($type in $uncovered) { Write-Output "Warning: plugin uses a type outside the compared list: $type" }
foreach ($comparison in $comparisons) {
    foreach ($line in $comparison.Review) { Write-Output "Review: $($comparison.Module) / $line" }
}
foreach ($comparison in $comparisons) {
    foreach ($line in $comparison.Breaking) { Write-Output "Breaking: $($comparison.Module) / $line" }
}
if ($breakingCount -gt 0) { throw 'Members the plugin uses are missing on one side. The plugin would fail to load or call them.' }
