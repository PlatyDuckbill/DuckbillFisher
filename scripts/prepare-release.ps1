#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][uri]$AssetBaseUrl,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/share')
)

# Local preparation only. Does not upload files or modify the live repository index.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$AssetBaseUrl.IsAbsoluteUri -or $AssetBaseUrl.Scheme -ne 'https' -or
    $AssetBaseUrl.UserInfo -or $AssetBaseUrl.Query -or $AssetBaseUrl.Fragment) {
    throw 'AssetBaseUrl must be an absolute HTTPS directory URL without credentials, query or fragment.'
}

$package = (Resolve-Path -LiteralPath $PackagePath).Path
$zip = [IO.Compression.ZipFile]::OpenRead($package)
$temp = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
try {
    # Tight allowlist for the current starter package; extend deliberately when adding assets.
    $required = @('DuckbillFisher.dll', 'DuckbillFisher.Core.dll', 'DuckbillFisher.json')
    $allowed = $required + @('DuckbillFisher.pdb', 'DuckbillFisher.Core.pdb', 'DuckbillFisher.deps.json', 'DuckbillFisher.Core.deps.json')
    $names = @($zip.Entries | ForEach-Object { $_.FullName })
    if (@($names | Select-Object -Unique).Count -ne $names.Count) { throw 'Duplicate ZIP entries.' }
    foreach ($name in $names) {
        if ($name -cnotin $allowed) { throw "Unexpected ZIP entry: $name. Review the packaging allowlist." }
    }
    foreach ($name in $required) {
        if ($name -cnotin $names) { throw "Missing ZIP entry: $name" }
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry('DuckbillFisher.json').Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable }
    finally { $reader.Dispose() }
    if ($manifest.InternalName -ne 'DuckbillFisher' -or $manifest.Name -ne 'DuckbillFisher' -or $manifest.DalamudApiLevel -ne 15) {
        throw 'Manifest identity/API does not match DuckbillFisher for D15.'
    }
    $version = [string]$manifest.AssemblyVersion
    if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Expected a four-component AssemblyVersion.' }
    $parsedVersion = [version]$version
    if ($parsedVersion -le [version]'0.0.0.0') { throw 'AssemblyVersion must be greater than zero.' }

    New-Item -ItemType Directory -Path $temp | Out-Null
    $assemblyPath = Join-Path $temp 'DuckbillFisher.dll'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('DuckbillFisher.dll'), $assemblyPath)
    $assembly = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
    if ($assembly.Name -ne 'DuckbillFisher' -or $assembly.Version -ne $parsedVersion) {
        throw 'Assembly identity/version differs from the manifest.'
    }
} finally {
    $zip.Dispose()
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

$output = [IO.Path]::GetFullPath($OutputDirectory)
$assetName = "DuckbillFisher-$version.zip"
$assetPath = Join-Path $output $assetName
$indexPath = Join-Path $output 'pluginmaster.json'
$hashPath = Join-Path $output "$assetName.sha256"
foreach ($path in @($assetPath, $indexPath, $hashPath)) {
    if (Test-Path -LiteralPath $path) { throw "Output already exists: $path. Use a new OutputDirectory." }
}
$downloadUrl = "$($AssetBaseUrl.AbsoluteUri.TrimEnd('/'))/$assetName"
$manifest.DownloadLinkInstall = $downloadUrl
$manifest.DownloadLinkUpdate = $downloadUrl
$manifest.LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$manifest.IsHide = $false
$manifest.IsTestingExclusive = $false
# Preserve SDK-generated metadata; this index contains one plugin.
$json = ConvertTo-Json -InputObject @($manifest) -Depth 20
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item -LiteralPath $package -Destination $assetPath
$hash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($indexPath, "$json`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($hashPath, "$hash  $assetName`n", [Text.UTF8Encoding]::new($false))
Write-Output "Prepared $assetPath and $indexPath. Nothing has been published."
