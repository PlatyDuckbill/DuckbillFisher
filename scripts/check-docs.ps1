#Requires -Version 7.0
[CmdletBinding()]
param()

# Repository documentation inspection only: no HTTP, build, tests or user notes.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$tracked = @(git -C $root -c core.quotepath=false ls-files -- '*.md')
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate tracked Markdown.' }
$files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($relative in $tracked) {
    if ([IO.Path]::GetFileName($relative) -eq 'tmp_note.md') { continue }
    $path = Join-Path $root $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) { [void]$files.Add([IO.Path]::GetFullPath($path)) }
}
# Include newly created project docs/skills before their first commit, never arbitrary root files.
foreach ($directory in @('docs', '.agents/skills')) {
    $path = Join-Path $root $directory
    if (!(Test-Path -LiteralPath $path)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*.md') {
        if ($file.Name -ne 'tmp_note.md') { [void]$files.Add($file.FullName) }
    }
}
$contents = @{}
foreach ($path in $files) { $contents[$path] = [IO.File]::ReadAllText($path) }
$problems = [Collections.Generic.List[string]]::new()
$links = 0
foreach ($path in $files) {
    $relative = [IO.Path]::GetRelativePath($root, $path).Replace('\', '/')
    if ($contents[$path] -match '(?i)\b[A-Z]:[\\/]Users[\\/][^\\/\s`"<>]+') {
        $problems.Add("${relative}: machine-specific user path; use DUCKBILL_ROOT, DALAMUD_HOME or a standard environment variable.")
    }
    $fence = ''
    $lineNumber = 0
    foreach ($line in ($contents[$path] -split '\r?\n')) {
        $lineNumber++
        if ($line -match '^\s*(`{3,}|~{3,})') {
            $marker = $Matches[1].Substring(0, 1)
            if (!$fence) { $fence = $marker } elseif ($fence -eq $marker) { $fence = '' }
            continue
        }
        if ($fence) { continue }
        # Inline Markdown links used by this repository. Reference-style links are not inspected.
        foreach ($match in [regex]::Matches($line, '\[[^\]]*\]\((?<target><[^>]+>|[^\s)]+)(?:\s+"[^"]*")?\)')) {
            $target = $match.Groups['target'].Value.Trim('<', '>')
            if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:|^//') { continue }
            $links++
            $parts = $target -split '#', 2
            $filePart = [uri]::UnescapeDataString(($parts[0] -split '\?', 2)[0])
            $destination = if (!$filePart) { $path } else {
                [IO.Path]::GetFullPath((Join-Path (Split-Path $path -Parent) $filePart))
            }
            if (!(Test-Path -LiteralPath $destination)) {
                $problems.Add("${relative}:${lineNumber}: missing path $target")
                continue
            }
            if ($parts.Count -lt 2 -or !$parts[1] -or !$contents.ContainsKey($destination)) { continue }
            $fragment = [uri]::UnescapeDataString($parts[1])
            # GitHub-style heading IDs for this repository's plain Markdown headings.
            $anchors = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            $counts = @{}
            $headingFence = ''
            foreach ($headingLine in ($contents[$destination] -split '\r?\n')) {
                if ($headingLine -match '^\s*(`{3,}|~{3,})') {
                    $marker = $Matches[1].Substring(0, 1)
                    if (!$headingFence) { $headingFence = $marker } elseif ($headingFence -eq $marker) { $headingFence = '' }
                    continue
                }
                if ($headingFence -or $headingLine -notmatch '^#{1,6}\s+(.+?)\s*#*\s*$') { continue }
                $slug = $Matches[1].ToLowerInvariant() -replace '[^\p{L}\p{M}\p{N}\s_\-]', '' -replace '\s', '-'
                if ($counts.ContainsKey($slug)) { $counts[$slug]++; $anchor = "$slug-$($counts[$slug])" }
                else { $counts[$slug] = 0; $anchor = $slug }
                [void]$anchors.Add($anchor)
            }
            if (!$anchors.Contains($fragment) -and $contents[$destination] -notmatch ('\bid=["'']' + [regex]::Escape($fragment) + '["'']')) {
                $problems.Add("${relative}:${lineNumber}: missing heading $target")
            }
        }
    }
}
foreach ($directory in @('verification', 'archive')) {
    $indexRoot = Join-Path $root "docs/$directory"
    $index = Join-Path $indexRoot 'README.md'
    if (!$contents.ContainsKey($index)) { $problems.Add("Missing $directory index."); continue }
    foreach ($file in Get-ChildItem -LiteralPath $indexRoot -Recurse -File -Filter '*.md') {
        if ($file.FullName -eq $index -or $file.Name -eq 'tmp_note.md') { continue }
        $relative = [IO.Path]::GetRelativePath($indexRoot, $file.FullName).Replace('\', '/')
        if (!$contents[$index].Contains("($relative)")) { $problems.Add("$directory index omits $relative") }
    }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File) {
    if ($file.Name -eq 'tmp_note.md') { continue }
    if ($file.Name -notin @('README.md', 'STATUS.md', 'SESSION-HANDOFF.md')) {
        $problems.Add("docs root contains $($file.Name); choose a product/development/verification/releases/archive folder.")
    }
}
foreach ($directory in Get-ChildItem -LiteralPath (Join-Path $root 'docs') -Directory) {
    if ($directory.Name -notin @('product', 'development', 'verification', 'releases', 'archive')) {
        $problems.Add("Unexpected docs folder $($directory.Name); use the documented structure.")
    }
}
foreach ($name in @('STATUS.md', 'SESSION-HANDOFF.md')) {
    $path = Join-Path $root "docs/$name"
    $lineCount = @($contents[$path].TrimEnd() -split '\r?\n').Count
    if ($lineCount -gt 70) { Write-Warning "$name has $lineCount lines; keep current context near 60 lines." }
}
# These local skills use single-line scalar frontmatter, not arbitrary YAML.
$skillCount = 0
foreach ($path in $files) {
    if ([IO.Path]::GetFileName($path) -ne 'SKILL.md' -or !$path.StartsWith((Join-Path $root '.agents/skills'), [StringComparison]::OrdinalIgnoreCase)) { continue }
    $skillCount++
    $body = $contents[$path]
    $folder = Split-Path (Split-Path $path -Parent) -Leaf
    if ($body -notmatch '\A---\r?\nname: (?<name>[a-z0-9]+(?:-[a-z0-9]+)*)\r?\ndescription: (?<description>[^\r\n]+)\r?\n---\r?\n') {
        $problems.Add("$folder skill: invalid single-line name/description frontmatter")
        continue
    }
    if ($Matches.name -cne $folder -or $Matches.name.Length -gt 64 -or $Matches.description.Length -gt 1024 -or $Matches.description -match '[<>]') {
        $problems.Add("$folder skill: invalid name or description")
    }
    if ($body -match '\[TODO:') { $problems.Add("$folder skill: unfinished placeholder") }
    $metadata = Join-Path (Split-Path $path -Parent) 'agents/openai.yaml'
    if (!(Test-Path -LiteralPath $metadata -PathType Leaf)) { $problems.Add("$folder skill: missing UI metadata"); continue }
    $metadataText = [IO.File]::ReadAllText($metadata)
    foreach ($key in @('display_name', 'short_description', 'default_prompt')) {
        if ($metadataText -notmatch "(?m)^  ${key}: `"([^`"\r\n]+)`"\r?$") { $problems.Add("$folder skill: missing quoted $key") }
    }
    if (!$metadataText.Contains('$' + $folder)) { $problems.Add("$folder skill: prompt does not name the skill") }
}
if ($problems.Count) {
    $problems | ForEach-Object { Write-Output $_ }
    throw "$($problems.Count) documentation issue(s)."
}
Write-Output "Documentation: $($files.Count) Markdown files, $links local links, verification/archive indexes, root layout and $skillCount local skill structures checked. No external URLs or product tests executed."
