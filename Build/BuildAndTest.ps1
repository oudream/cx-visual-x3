param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$ShowGUI
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$log = Join-Path $root ('Output\' + $Configuration + '\x64\Build.log')
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$msbuild = (Get-Command msbuild.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
if (-not $msbuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw 'MSBuild not found. Install Visual Studio 2019/2022 with .NET Framework 4.8 targeting pack and WPF build tools.'
}
Write-Host "MSBuild: $msbuild"
& $msbuild (Join-Path $root 'CxVisual.Unified.sln') /m:1 /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=x64 /nologo /v:minimal "/flp:logfile=$log;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE). See $log" }
& (Join-Path $PSScriptRoot 'Stage-RuntimeDlls.ps1') -Configuration $Configuration
& (Join-Path $PSScriptRoot 'Run-SmokeTests.ps1') -Configuration $Configuration -ShowGUI:$ShowGUI
if ($LASTEXITCODE -ne 0) { throw 'Smoke test failed' }
