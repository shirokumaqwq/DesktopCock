param([int]$Minutes = 30)
$ErrorActionPreference = 'Stop'
if ($Minutes -lt 1) { throw 'Minutes must be positive' }
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'dist\DesktopCock-win-x64\DesktopCock.exe'
if (-not (Test-Path $exe)) { throw 'Publish the application first.' }
$running = Get-Process DesktopCock -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Select-Object -First 1
if (-not $running) { $running = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru }
$reportDir = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $reportDir | Out-Null
$file = Join-Path $reportDir 'soak.csv'
if (Test-Path $file) { $file = Join-Path $reportDir ("soak-{0}.csv" -f (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$samples = [System.Collections.Generic.List[object]]::new()
$clock = [Diagnostics.Stopwatch]::StartNew()
while ($clock.Elapsed.TotalMinutes -lt $Minutes) {
    $running.Refresh()
    if ($running.HasExited) { throw 'DesktopCock exited during soak test.' }
    $sample = [pscustomobject]@{
        Seconds = [math]::Round($clock.Elapsed.TotalSeconds,1)
        PrivateMB = [math]::Round($running.PrivateMemorySize64 / 1MB,2)
        WorkingSetMB = [math]::Round($running.WorkingSet64 / 1MB,2)
        Handles = $running.HandleCount
        CpuSeconds = $running.TotalProcessorTime.TotalSeconds
    }
    $samples.Add($sample)
    $sample | Export-Csv -LiteralPath $file -Append -NoTypeInformation
    Start-Sleep -Seconds 10
}
$warm = $samples | Where-Object Seconds -ge ([math]::Min(120,$Minutes*10))
$half = [math]::Floor($warm.Count / 2)
$first = ($warm | Select-Object -First $half | Measure-Object PrivateMB -Average).Average
$last = ($warm | Select-Object -Last $half | Measure-Object PrivateMB -Average).Average
$summary = [pscustomobject]@{
    Minutes = $Minutes
    Samples = $samples.Count
    FirstHalfPrivateMBAverage = [math]::Round($first,2)
    LastHalfPrivateMBAverage = [math]::Round($last,2)
    PrivateMBChange = [math]::Round($last-$first,2)
    HandleChange = $samples[-1].Handles-$samples[0].Handles
    Status = 'Completed; inspect memory/handle trends. The pet remains running.'
}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDir 'soak-summary.json') -Encoding utf8
$summary
