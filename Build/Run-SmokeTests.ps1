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
if ($ShowGUI) {
    $studio = Join-Path $out 'CxVisual.Studio.exe'
    if (-not (Test-Path $studio)) { throw 'Missing Studio app' }
    Start-Process -FilePath $studio
    Write-Host 'GUI started. Manual actions: Add / Drag / Undo / Redo / Save / Load.'
}
Write-Host 'CLI + unit smoke tests passed.'
