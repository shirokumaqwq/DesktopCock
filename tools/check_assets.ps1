$ErrorActionPreference = 'Stop'
$assetRoot = Split-Path $PSScriptRoot -Parent
function Get-AssetHash([string]$Path) {
    $assetHasher = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($assetHasher.ComputeHash([System.IO.File]::ReadAllBytes($Path))).Replace('-', '').ToLowerInvariant() }
    finally { $assetHasher.Dispose() }
}
$assetFolder = Join-Path $assetRoot 'src\DesktopCock\Assets'
$assetManifest = Get-Content -LiteralPath (Join-Path $assetFolder 'animations.json') -Raw | ConvertFrom-Json
if (-not $assetManifest.sourceHashes) { throw 'Assets have no source inventory. Run tools/prepare_assets.py.' }
function Get-AssetImpact([string]$Source) {
    $entry = $assetManifest.sourceImpact.PSObject.Properties[$Source]
    if ($entry) {
        return "Affected clips: $($entry.Value.clips -join ', '); skin regions: $($entry.Value.skinRegions -join ', '); previews: $($entry.Value.previews -join ', ')."
    }
    return "Shared declaration or generator changed. Affected clips/previews: $($assetManifest.clips.PSObject.Properties.Name -join ', '); review every frame's skin regions."
}
foreach ($assetInput in $assetManifest.sourceHashes.PSObject.Properties) {
    $assetInputPath = Join-Path $assetRoot $assetInput.Name
    if (-not (Test-Path -LiteralPath $assetInputPath) -or
        (Get-AssetHash $assetInputPath) -ne $assetInput.Value) {
        throw "Asset source changed: $($assetInput.Name). $(Get-AssetImpact $assetInput.Name) Run tools/prepare_assets.py after review."
    }
}
if (-not $assetManifest.generatedDocuments) { throw 'Missing generated animation reference document.' }
foreach ($document in $assetManifest.generatedDocuments.PSObject.Properties) {
    $documentPath = Join-Path $assetRoot $document.Name
    if (-not (Test-Path -LiteralPath $documentPath) -or (Get-AssetHash $documentPath) -ne $document.Value) {
        throw "Animation documentation is out of date: $($document.Name). Run tools/prepare_assets.py."
    }
}
$expectedAssetFiles = @()
foreach ($assetFrame in $assetManifest.frames.PSObject.Properties) {
    $assetFramePath = Join-Path $assetFolder $assetFrame.Value.file
    if (-not $assetFrame.Value.sha256 -or -not (Test-Path -LiteralPath $assetFramePath) -or
        (Get-AssetHash $assetFramePath) -ne $assetFrame.Value.sha256) {
        throw "Generated frame is out of date: $($assetFrame.Name). Regenerate the complete asset set."
    }
    $expectedAssetFiles += [System.IO.Path]::GetFullPath($assetFramePath)
}
foreach ($assetFile in Get-ChildItem -LiteralPath $assetFolder -Filter '*.png' -Recurse -File) {
    if ($assetFile.FullName -notin $expectedAssetFiles) { throw "Unreferenced runtime PNG: $($assetFile.FullName)" }
}
$skinPath = Join-Path $assetFolder 'skins.json'
if (-not (Test-Path -LiteralPath $skinPath) -or -not $assetManifest.skinManifestSha256 -or
    (Get-AssetHash $skinPath) -ne $assetManifest.skinManifestSha256) {
    throw 'Skin manifest is missing or out of date. Run tools/prepare_assets.py.'
}
$skinManifest = Get-Content -LiteralPath $skinPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($skinManifest.schemaVersion -ne 1 -or -not $skinManifest.sourceHashes) { throw 'Invalid skin manifest.' }
foreach ($skinInput in $skinManifest.sourceHashes.PSObject.Properties) {
    $skinInputPath = Join-Path $assetRoot $skinInput.Name
    if (-not (Test-Path -LiteralPath $skinInputPath) -or (Get-AssetHash $skinInputPath) -ne $skinInput.Value) {
        throw "Skin source changed: $($skinInput.Name). Run tools/prepare_skins.py and review its previews."
    }
}
if (@($skinManifest.frames.PSObject.Properties).Count -ne @($assetManifest.frames.PSObject.Properties).Count) {
    throw 'Skin masks must cover every animation frame.'
}
foreach ($assetFrame in $assetManifest.frames.PSObject.Properties) {
    $skinFrame = $skinManifest.frames.PSObject.Properties[$assetFrame.Name].Value
    if (-not $skinFrame -or $skinFrame.sha256 -ne $assetFrame.Value.sha256 -or
        @($skinFrame.rows).Count -ne $assetFrame.Value.height -or @($skinFrame.rows | Where-Object Length -ne $assetFrame.Value.width).Count -ne 0) {
        throw "Skin mask is out of date: $($assetFrame.Name). Review regions and regenerate skins."
    }
}
Write-Output "Assets current: $($expectedAssetFiles.Count) frames; source and output hashes match."
Write-Output "Skins current: $(@($skinManifest.skins.PSObject.Properties).Count) palettes; all frame masks match."
