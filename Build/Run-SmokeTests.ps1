param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug', [switch]$ShowGUI)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root ('Output\' + $Configuration + '\x64')
function Invoke-Sample([string]$file, [string[]]$arguments = @()) {
    $exe = Join-Path $out $file
    if (-not (Test-Path $exe)) { throw "Missing $exe — build solution first" }
    Write-Host "RUN $file $($arguments -join ' ')"
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file exit code $LASTEXITCODE" }
}
Invoke-Sample 'CxVisual.Tests.FoundationSmoke.exe'
Invoke-Sample 'CxVisual.Tests.ArchitectureSmoke.exe'
Invoke-Sample 'CxVisual.Runtime.Host.exe' @('demo')
$path = Join-Path $out 'Data\ci-smoke.xml'
Invoke-Sample 'CxVisual.Runtime.Host.exe' @('save', $path)
Invoke-Sample 'CxVisual.Runtime.Host.exe' @('load', $path)
# WPF WinExe: use Start-Process -Wait and its ExitCode; do not rely on PowerShell's LASTEXITCODE.
$testCenter = Join-Path $out 'CxVisual.TestCenter.exe'
$testReport = Join-Path $out 'Data\\testcenter-smoke.txt'
New-Item -ItemType Directory -Force (Split-Path $testReport) | Out-Null
if (-not (Test-Path $testCenter)) { throw "Missing $testCenter — build solution first" }
$testCenterProcess = Start-Process -FilePath $testCenter -WorkingDirectory $root -ArgumentList @('--run','--report',("`"" + $testReport + "`"")) -PassThru -Wait
if ($testCenterProcess.ExitCode -ne 0) { throw "TestCenter failed (exit $($testCenterProcess.ExitCode)); report: $testReport" }
if (-not (Test-Path $testReport)) { throw "TestCenter did not write report: $testReport" }
Write-Host "PASS TestCenter: $testReport"
if ($ShowGUI) {
    Start-Process -FilePath $testCenter
    Write-Host 'TestCenter GUI started. Manual checks: Explorer / Run / Assertions / Inspector.'
    $studio = Join-Path $out 'CxVisual.Studio.exe'
    if (-not (Test-Path $studio)) { throw 'Missing Studio app' }
    Start-Process -FilePath $studio
    Write-Host 'GUI started. Manual actions: Add / Drag / Undo / Redo / Save / Load.'
    $foundationWpf = Join-Path $out 'CxVisual.Tests.FoundationWpf.exe'
    if (-not (Test-Path $foundationWpf)) { throw 'Missing FoundationWpf test app' }
    Start-Process -FilePath $foundationWpf
    Write-Host 'FoundationWpf started. Manually exercise Logging/System/Stress tabs.'
}
Write-Host 'CLI + unit smoke tests passed.'
