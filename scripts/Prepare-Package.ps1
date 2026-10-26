# Prepares a NEW directory from an already published output. Does not build,
# execute binaries, download files, call GitHub or certify licensing.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$Destination,
    [Parameter(Mandatory = $true)][string]$RuntimeProvenance,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceRevision,
    [string]$ToolsDirectory,
    [string]$FfmpegProvenance
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$policy = Get-Content -LiteralPath (Join-Path $repo 'docs\package-manifest.json') -Raw | ConvertFrom-Json

function Read-Object([string]$Path) {
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}
function Require-Text($Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace([string]$Value) -or ([string]$Value).Contains('REPLACE_ME')) {
        throw "Missing actual provenance: $Name"
    }
}
function Plain-File([string]$Path) {
    $file = Get-Item -LiteralPath $Path -Force
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Expected a regular file, not a directory/reparse point: $Path"
    }
    return $file.FullName
}
function Under-Root([string]$Root, [string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw "Absolute package path: $Relative" }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes package root: $Relative" }
    return $full
}
$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
if (-not [IO.Directory]::Exists($publish)) { throw 'PublishDirectory must be a filesystem directory.' }
if (Test-Path -LiteralPath $target) { throw 'Destination must not exist. No existing package is overwritten.' }
$parent = Split-Path -Parent $target
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Create the destination parent directory first.' }
$prefix = $publish.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if ($target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destination must be outside publish output.' }
$runtime = Read-Object $RuntimeProvenance
Require-Text $runtime.version 'runtime.version'
Require-Text $runtime.origin 'runtime.origin'
Require-Text $runtime.variant 'runtime.variant'
if ([bool]$ToolsDirectory -ne [bool]$FfmpegProvenance) { throw 'Supply both ToolsDirectory and FfmpegProvenance, or neither.' }
$tools = $null
if ($ToolsDirectory) {
    $tools = Read-Object $FfmpegProvenance
    foreach ($field in @('version', 'origin', 'variant', 'configuration')) { Require-Text $tools.$field "ffmpeg.$field" }
}

# Reject unexpected runtime dependencies instead of making an incomplete package.
$known = @{}
foreach ($relative in $policy.requiredPublishedFiles) { $known[$relative.Replace('\', '/').ToLowerInvariant()] = $true }
foreach ($entry in Get-ChildItem -LiteralPath $publish -Recurse -Force) {
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point in publish output: $($entry.Name)" }
    if ($entry.PSIsContainer) { continue }
    $relative = $entry.FullName.Substring($prefix.Length).Replace('\', '/')
    if ($policy.ignoredPublishedExtensions -contains $entry.Extension.ToLowerInvariant()) { continue }
    if (-not $known.ContainsKey($relative.ToLowerInvariant())) { throw "Unexpected published file: $relative. Review single-file output/manifest; do not silently omit dependencies." }
}
$stage = Join-Path $parent ('.webmcut-package-' + [Guid]::NewGuid().ToString('N'))
$created = $false
$components = New-Object System.Collections.Generic.List[object]
try {
    New-Item -ItemType Directory -Path $stage -ErrorAction Stop | Out-Null
    $created = $true
    function Copy-Component([string]$Source, [string]$Relative, [string]$Role) {
        $file = Plain-File $Source
        $destinationFile = Under-Root $stage $Relative
        $folder = Split-Path -Parent $destinationFile
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Copy-Item -LiteralPath $file -Destination $destinationFile
        $components.Add([ordered]@{
            path = $Relative.Replace('\', '/'); role = $Role
            sha256 = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash.ToLowerInvariant()
            bytes = (Get-Item -LiteralPath $destinationFile).Length
        })
    }
    foreach ($relative in $policy.requiredPublishedFiles) {
        Copy-Component (Under-Root $publish $relative) $relative 'application-or-documentation'
    }
    foreach ($name in $policy.runtimeDocuments) {
        # documents is an object whose keys are the exact required target filenames.
        $property = $runtime.documents.PSObject.Properties[$name]
        if ($null -eq $property) { throw "Missing runtime document: $name" }
        Copy-Component ([string]$property.Value) "legal/runtime/$name" 'runtime-notice'
    }
    $toolsInfo = $null
    if ($null -ne $tools) {
        foreach ($name in $policy.optionalTools) { Copy-Component (Join-Path $ToolsDirectory $name) "tools/$name" 'external-tool' }
        foreach ($name in $policy.toolsDocuments) {
            $property = $tools.documents.PSObject.Properties[$name]
            if ($null -eq $property) { throw "Missing FFmpeg document: $name" }
            Copy-Component ([string]$property.Value) "legal/ffmpeg/$name" 'tool-notice'
        }
        $toolsInfo = [ordered]@{ version = $tools.version; origin = $tools.origin; variant = $tools.variant; configuration = $tools.configuration }
    }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage $policy.application)).ProductVersion
    Require-Text $version 'application.ProductVersion'
    $manifest = [ordered]@{
        schemaVersion = 1; sourceRevision = $SourceRevision.ToLowerInvariant()
        applicationProductVersion = $version; preparedAtUtc = [DateTime]::UtcNow.ToString('o')
        runtime = [ordered]@{ version = $runtime.version; origin = $runtime.origin; variant = $runtime.variant }
        tools = $toolsInfo; files = @($components.ToArray())
        verification = 'File inventory and supplied provenance only; binaries not executed; not legal clearance.'
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage $policy.generatedManifest) -Encoding UTF8
    [IO.Directory]::Move($stage, $target)
    $created = $false
    Write-Output $target
}
finally {
    if ($created -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
