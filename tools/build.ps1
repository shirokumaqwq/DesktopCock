param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $env:LOCALAPPDATA 'DesktopCock\dotnet\dotnet.exe'
$sdk = if (Test-Path $localSdk) { $localSdk } else { 'dotnet' }
Push-Location $root
try {
    & $sdk build src/DesktopCock -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Publish) {
        $distributionRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'dist'))
        $packageName = "DesktopCock-win-x64-$Configuration"
        $packagePath = [System.IO.Path]::GetFullPath((Join-Path $distributionRoot $packageName))
        $stagingPath = [System.IO.Path]::GetFullPath((Join-Path $distributionRoot ".stage-$packageName"))
        # Only these generated, direct children of dist may ever be replaced.
        foreach ($generatedPath in @($stagingPath, $packagePath)) {
            if ([System.IO.Path]::GetDirectoryName($generatedPath) -ne $distributionRoot) {
                throw "Unsafe output path: $generatedPath"
            }
            if ((Test-Path -LiteralPath $generatedPath) -and
                ((Get-Item -LiteralPath $generatedPath).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                throw "Output cannot be a link: $generatedPath"
            }
        }
        if (Test-Path -LiteralPath $stagingPath) { Remove-Item -LiteralPath $stagingPath -Recurse -Force }
        & $sdk publish src/DesktopCock -c $Configuration -r win-x64 --self-contained true -o $stagingPath
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        Copy-Item -LiteralPath (Join-Path $root 'docs/USER_GUIDE.md') -Destination (Join-Path $stagingPath 'README.md')
        Copy-Item -LiteralPath (Join-Path $root 'docs/AUDIO.md') -Destination (Join-Path $stagingPath 'AUDIO.md')
        Copy-Item -LiteralPath (Join-Path $root 'tools/setup_speech.ps1') -Destination (Join-Path $stagingPath 'Install-Speech.ps1')
        if ($Configuration -eq 'Debug') {
            Copy-Item -LiteralPath (Join-Path $root 'docs/DEBUGGING.md') -Destination (Join-Path $stagingPath 'DEBUGGING.md')
        }
        $forbidden = Get-ChildItem -LiteralPath $stagingPath -Recurse -File |
            Where-Object { $_.Name -match '(?i)(tests?|validation|verification|\.cs$|\.ps1$|\.py$)' -and
                $_.FullName -ne (Join-Path $stagingPath 'Install-Speech.ps1') }
        if ($forbidden) { throw "Unexpected development files in package: $($forbidden.FullName -join ', ')" }
        if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Recurse -Force }
        Move-Item -LiteralPath $stagingPath -Destination $packagePath
        $packageItems = Get-ChildItem -LiteralPath $packagePath
        Compress-Archive -LiteralPath $packageItems.FullName -DestinationPath "$packagePath.zip" -Force
        Write-Output "Published $Configuration package: $packagePath.zip"
    }
} finally { Pop-Location }
