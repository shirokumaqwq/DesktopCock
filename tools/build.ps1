param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $env:LOCALAPPDATA 'DesktopCock\dotnet\dotnet.exe'
$sdk = if (Test-Path $localSdk) { $localSdk } else { 'dotnet' }
Push-Location $root
try {
    & $sdk run --project tests/DesktopCock.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed' }
    & $sdk build src/DesktopCock -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Publish) {
        & $sdk publish src/DesktopCock -c Release -r win-x64 --self-contained true -o dist/DesktopCock-win-x64
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        Copy-Item README.md dist/DesktopCock-win-x64/README.md
        if (Test-Path VALIDATION.md) { Copy-Item VALIDATION.md dist/DesktopCock-win-x64/VALIDATION.md }
        Compress-Archive -Path dist/DesktopCock-win-x64/* -DestinationPath dist/DesktopCock-win-x64.zip -Force
    }
} finally { Pop-Location }
