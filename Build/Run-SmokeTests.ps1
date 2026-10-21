param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug', [switch]$ShowGUI)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root ('Output\' + $Configuration + '\x64')
function Invoke-Sample([string]$file, [string[]]$arguments = @()) {
    $exe = Join-Path $out $file
    if (-not (Test-Path $exe)) { throw "Missing $exe - build solution first" }
    Write-Host "RUN $file $($arguments -join ' ')"
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file exit code $LASTEXITCODE" }
}
Invoke-Sample 'CxVisual.Tests.ArchitectureSmoke.exe'
Invoke-Sample 'CxVisual.Tests.ArchitectureSmoke.exe' @('--concurrency')
Invoke-Sample 'CxVisual.Tests.ArchitectureSmoke.exe' @('--config-foundation')
Invoke-Sample 'CxVisual.Runtime.Host.exe' @('demo')
$path = Join-Path $out 'Data\ci-smoke.xml'
Invoke-Sample 'CxVisual.Runtime.Host.exe' @('save', $path)
Invoke-Sample 'CxVisual.Runtime.Host.exe' @('load', $path)
if ($ShowGUI) {
    $studio = Join-Path $out 'CxVisual.Studio.exe'
    if (-not (Test-Path $studio)) { throw 'Missing Studio app' }
    Start-Process -FilePath $studio
    Write-Host 'GUI started. Manual actions: Add / Drag / Undo / Redo / Save / Load.'
    Write-Host 'Foundation logging and concurrency are tested via ArchitectureSmoke; legacy FoundationWpf GUI has been removed.'
}
Write-Host 'CLI + unit smoke tests passed.'

# TCT: run real Foundation/Mechanism + framework-contract tests in isolated workers.
$testCenter = Join-Path $out 'CxVisual.TestCenter.exe'
if (-not (Test-Path $testCenter)) { throw "Missing $testCenter; build CxVisual.Unified.sln first" }
$report = Join-Path $out 'TestCenter.junit.xml'
$worker = Start-Process -FilePath $testCenter -WorkingDirectory $root -ArgumentList @('--run','--report',('"' + $report + '"'),'--format','junit','--isolate') -PassThru -Wait
if ($worker.ExitCode -ne 0) { throw "TestCenter failed: $($worker.ExitCode); inspect $report" }
if (-not (Test-Path $report)) { throw "TestCenter did not write JUnit report" }
Write-Host "PASS TestCenter: $report"
