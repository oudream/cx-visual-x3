#requires -version 5.1
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$sln = Join-Path $root 'CxVisual.AvalonDock.PoC.sln'
$msbuild = (Get-Command msbuild.exe -ErrorAction Stop).Source
Write-Host "[CxVisual AvalonDock PoC] MSBuild: $msbuild"
Write-Host "[CxVisual AvalonDock PoC] Restore 5.0.1 packages + Build .NET48 x64"
& $msbuild $sln '/t:Restore,Build' '/m:1' '/nr:false' "/p:Configuration=$Configuration" '/p:Platform=x64' '/p:RestoreIgnoreFailedSources=false' '/v:minimal'
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit=$LASTEXITCODE" }
if ($BuildOnly) { Write-Host '[OK] Build passed (tests NOT executed).'; exit 0 }

$exe = Join-Path $root "src\CxVisual.AvalonDock.PoC\bin\$Configuration\x64\CxVisual.AvalonDock.PoC.exe"
$report = Join-Path $root 'Tests\SelfTestResult.txt'
if (Test-Path $report) { Remove-Item $report -Force }
if (-not (Test-Path $exe)) { throw "WPF EXE missing: $exe" }
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test', "`"$report`"") -PassThru -Wait
if (-not (Test-Path $report)) { throw 'GUI self-test did not produce report; check WPF exception or app startup.' }
Get-Content $report | ForEach-Object { Write-Host $_ }
if ($process.ExitCode -ne 0 -or (Select-String -Path $report -Pattern '^FAIL:')) {
    throw "WPF STA self-tests failed. ExitCode=$($process.ExitCode)"
}
$passCount = @(Select-String -Path $report -Pattern '^PASS:').Count
if ($passCount -ne 10) { throw "Expected exactly 10 tests; got $passCount" }
Write-Host '[OK] .NET48 x64 build + 10 WPF STA tests passed.'
