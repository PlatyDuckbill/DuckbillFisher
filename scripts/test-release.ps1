#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'package-files.ps1')
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
$prepare = Join-Path $PSScriptRoot 'prepare-release.ps1'
$baseUrl = 'https://example.invalid/releases/v0.1.0.0'
New-Item -ItemType Directory -Path $temp | Out-Null

function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw 'Expected invalid input to be rejected.' }
}

try {
    $validOutput = Join-Path $temp 'valid'
    & $prepare -PackagePath $package -AssetBaseUrl $baseUrl -OutputDirectory $validOutput
    $indexPath = Join-Path $validOutput 'pluginmaster.json'
    $indexText = Get-Content $indexPath -Raw
    $index = ConvertFrom-Json -InputObject $indexText -NoEnumerate
    if ($index -isnot [array] -or $index.Count -ne 1) { throw 'Index must be a JSON array with one entry.' }
    $entry = $index[0]
    if ($entry.InternalName -ne 'DuckbillFisher' -or $entry.DownloadLinkInstall -ne $entry.DownloadLinkUpdate) {
        throw 'Incorrect repository entry.'
    }
    $asset = Join-Path $validOutput "DuckbillFisher-$($entry.AssemblyVersion).zip"
    if ((Get-FileHash $asset).Hash -ne (Get-FileHash $package).Hash) { throw 'ZIP content changed.' }
    Assert-Rejected { & $prepare -PackagePath $package -AssetBaseUrl $baseUrl -OutputDirectory $validOutput }
    if ((Get-Content $indexPath -Raw) -ne $indexText) { throw 'Existing index was modified.' }
    Assert-Rejected { & $prepare -PackagePath $package -AssetBaseUrl 'http://example.invalid/' -OutputDirectory (Join-Path $temp 'bad-url') }

    $satellite = @(Get-RequiredPluginFiles | Where-Object { $_.EndsWith('/DuckbillFisher.resources.dll') })[0]
    foreach ($case in @('missing-core', 'runtime-dll', 'wrong-api', 'wrong-version', 'duplicate', 'missing-localization', 'normalized-duplicate')) {
        $bad = Join-Path $temp "$case.zip"
        Copy-Item $package $bad
        $zip = [IO.Compression.ZipFile]::Open($bad, [IO.Compression.ZipArchiveMode]::Update)
        try {
            switch ($case) {
                'missing-core' { $zip.GetEntry('DuckbillFisher.Core.dll').Delete() }
                'runtime-dll' { $zip.CreateEntry('Dalamud.dll') | Out-Null }
                'duplicate' { $zip.CreateEntry('DuckbillFisher.json') | Out-Null }
                'missing-localization' {
                    $localeEntry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $satellite }
                    $localeEntry.Delete()
                }
                'normalized-duplicate' {
                    $localeEntry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $satellite }
                    $alternate = if ($localeEntry.FullName.Contains('\')) { $satellite } else { $satellite.Replace('/', '\') }
                    $zip.CreateEntry($alternate) | Out-Null
                }
                default {
                    $reader = [IO.StreamReader]::new($zip.GetEntry('DuckbillFisher.json').Open())
                    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable }
                    finally { $reader.Dispose() }
                    $zip.GetEntry('DuckbillFisher.json').Delete()
                    if ($case -eq 'wrong-api') { $manifest.DalamudApiLevel = 99 }
                    else { $manifest.AssemblyVersion = '9.9.9.9' }
                    $writer = [IO.StreamWriter]::new($zip.CreateEntry('DuckbillFisher.json').Open())
                    try { $writer.Write(($manifest | ConvertTo-Json -Depth 20)) }
                    finally { $writer.Dispose() }
                }
            }
        } finally { $zip.Dispose() }
        $badOutput = Join-Path $temp "out-$case"
        Assert-Rejected { & $prepare -PackagePath $bad -AssetBaseUrl $baseUrl -OutputDirectory $badOutput }
        if (Test-Path $badOutput) { throw "Invalid package wrote output: $case" }
    }
    Write-Output 'PASS: valid release, overwrite protection, HTTPS, missing dependency, runtime exclusion, API, version, duplicate entries, missing localization, normalized duplicate paths.'
} finally {
    $resolvedTemp = (Resolve-Path -LiteralPath $temp).Path
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolvedTemp) -ne $tempParent) { throw 'Refusing cleanup outside the temporary root.' }
    Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
}
