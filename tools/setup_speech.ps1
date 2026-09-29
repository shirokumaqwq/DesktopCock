param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'DesktopCock\Speech'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$speechRoot = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $speechRoot | Out-Null
$downloads = @(
    @{ Name='whisper-bin-x64.zip'; Url='https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.2/whisper-bin-x64.zip'; Sha='49dcc16de826f20bd53d44f947a1ae49dfa81f86cad67a64d80820cb192d674a' },
    @{ Name='ggml-base.bin'; Url='https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base.bin'; Sha='60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe' },
    @{ Name='ggml-silero-v6.2.0.bin'; Url='https://huggingface.co/ggml-org/whisper-vad/resolve/9ffd54a1e1ee413ddf265af9913beaf518d1639b/ggml-silero-v6.2.0.bin'; Sha='2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987' }
)
foreach ($item in $downloads) {
    $target = Join-Path $speechRoot $item.Name
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $item.Sha) { continue }
    Write-Output "Downloading $($item.Name)..."
    Invoke-WebRequest -Uri $item.Url -OutFile "$target.download" -UseBasicParsing
    if ((Get-FileHash -LiteralPath "$target.download" -Algorithm SHA256).Hash -ne $item.Sha) { throw "SHA256 mismatch: $($item.Name)" }
    Move-Item -LiteralPath "$target.download" -Destination $target -Force
}
$unpack = Join-Path $speechRoot ('unpack-'+[Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath (Join-Path $speechRoot 'whisper-bin-x64.zip') -DestinationPath $unpack
$executable = Get-ChildItem -LiteralPath $unpack -Recurse -Filter whisper-cli.exe -File | Select-Object -First 1
if (!$executable) { throw 'Official archive does not contain whisper-cli.exe' }
Get-ChildItem -LiteralPath $executable.DirectoryName -File |
    Where-Object { $_.Extension -eq '.dll' -or $_.Name -eq 'whisper-cli.exe' } |
    Copy-Item -Destination $speechRoot -Force
$licenses = @(
    @{ Name='whisper.cpp-LICENSE.txt'; Url='https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.9.2/LICENSE' },
    @{ Name='Whisper-LICENSE.txt'; Url='https://raw.githubusercontent.com/openai/whisper/main/LICENSE' },
    @{ Name='Silero-LICENSE.txt'; Url='https://raw.githubusercontent.com/snakers4/silero-vad/master/LICENSE' }
)
foreach ($license in $licenses) { Invoke-WebRequest -Uri $license.Url -OutFile (Join-Path $speechRoot $license.Name) -UseBasicParsing }
$downloads | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $speechRoot 'sources.json') -Encoding UTF8
Write-Output "Local speech components installed: $speechRoot"
Write-Output 'No microphone was opened. Teaching is enabled only by pressing the button in the app.'
