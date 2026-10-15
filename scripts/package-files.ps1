# Shared package contract for build/development copy and release preparation.
function Get-RequiredPluginFiles {
    'DuckbillFisher.dll'
    'DuckbillFisher.Core.dll'
    'DuckbillFisher.json'
    $resourceRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/DuckbillFisher.Plugin/Localization'
    foreach ($resource in Get-ChildItem -LiteralPath $resourceRoot -Filter 'Strings.*.resx') {
        $culture = $resource.BaseName.Substring('Strings.'.Length)
        if ([Globalization.CultureInfo]::GetCultureInfo($culture).Name -cne $culture) {
            throw "Noncanonical resource culture: $culture"
        }
        "$culture/DuckbillFisher.resources.dll"
    }
}
