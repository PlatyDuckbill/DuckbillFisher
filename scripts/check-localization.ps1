#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$resourceRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/DuckbillFisher.Plugin/Localization'

function Read-Strings([string]$Path) {
    [xml]$document = Get-Content -Raw -Encoding utf8 -LiteralPath $Path
    $values = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $document.root.data) {
        $key = $entry.GetAttribute('name')
        if (!$values.TryAdd($key, [string]$entry.value)) { throw "Duplicate resource key: $key ($Path)" }
        # Validate composite-format syntax, including escaped braces, without a game dependency.
        [string]::Format([Globalization.CultureInfo]::InvariantCulture, [string]$entry.value, [object[]](0..31)) | Out-Null
    }
    return ,$values
}

function Get-ArgumentIndexes([string]$Value) {
    return (@([regex]::Matches($Value, '(?<!\{)\{([0-9]+)(?:[^{}]*)\}') |
        ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique) -join ',')
}

$neutral = Read-Strings (Join-Path $resourceRoot 'Strings.resx')
$enumText = Get-Content -Raw -LiteralPath (Join-Path $resourceRoot 'TextKey.cs')
$keys = @([regex]::Matches($enumText, '(?m)^\s*(\w+),\s*$') | ForEach-Object { $_.Groups[1].Value })
if ($keys.Count -ne $neutral.Count) { throw 'TextKey and neutral resource key counts differ.' }
foreach ($key in $keys) {
    if (!$neutral.ContainsKey($key)) { throw "Missing neutral resource: $key" }
}
$translations = @(Get-ChildItem -LiteralPath $resourceRoot -Filter 'Strings.*.resx')
foreach ($file in $translations) {
    $translated = Read-Strings $file.FullName
    if ($translated.Count -ne $neutral.Count) { throw "Resource key counts differ: $($file.Name)" }
    foreach ($key in $keys) {
        if (!$translated.ContainsKey($key)) { throw "Missing translation: $key ($($file.Name))" }
        if ((Get-ArgumentIndexes $neutral[$key]) -ne (Get-ArgumentIndexes $translated[$key])) {
            throw "Format arguments differ: $key ($($file.Name))"
        }
    }
}
Write-Output "Localization: $($keys.Count) typed keys, $($translations.Count) translation file(s), matching format arguments."
