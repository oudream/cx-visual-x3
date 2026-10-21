param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    $msbuild=(Get-Command msbuild.exe -ErrorAction Stop).Source
    $flags=@('/m:1','/t:Rebuild',('/p:Configuration='+$Configuration),'/p:Platform=x64','/nologo','/v:minimal')
    foreach($project in @('Foundation/Data/CxVisual.Foundation.Data.csproj','Mechanism/Data/CxVisual.Mechanism.Data.csproj')) {
        & $msbuild $project @flags
        if($LASTEXITCODE -ne 0){throw "Independent config owner build failed: $project"}
    }
    $bin=Split-Path $msbuild
    $csc=Join-Path $bin 'Roslyn/csc.exe'
    if(-not (Test-Path $csc)){$csc=Join-Path (Split-Path $bin) 'Roslyn/csc.exe'}
    if(-not (Test-Path $csc)){throw 'Roslyn compiler not found'}
    $out=Join-Path $root ('Output/'+$Configuration+'/x64')
    New-Item -Path $out -ItemType Directory -Force | Out-Null
    $entry=Join-Path $out 'ConfigIsolationMain.generated.cs'
    @'
namespace CxVisual.Tests.ConfigFoundationSmoke {
    internal static class ConfigIsolationMain {
        private static int Main(string[] args) { return ConfigFoundationSmokeRunner.Run(args); }
    }
}
'@ | Set-Content -LiteralPath $entry -Encoding UTF8
    $folder=Join-Path $root 'Tests/ArchitectureSmoke/Configuration'
    $sources=@('ConfigurationIntegrationTests.cs','ConfigV13ExtensionTests.cs','ConfigArchitectureCompletionTests.cs',
        'ConfigDeveloperExperienceTests.cs','ConfigPerformanceTests.cs','ConfigFoundationSmokeRunner.cs') |
        ForEach-Object { Join-Path $folder $_ }
    foreach($f in $sources){if(-not (Test-Path $f)){throw "Missing independent config source: $f"}}
    $args=@('/nologo','/target:exe','/platform:x64','/langversion:7.3',
        ('/out:'+(Join-Path $out 'CxVisual.ConfigIsolation.exe')),
        '/reference:System.dll','/reference:System.Core.dll','/reference:System.ComponentModel.DataAnnotations.dll',
        ('/reference:'+(Join-Path $out 'CxVisual.Foundation.Data.dll')),
        ('/reference:'+(Join-Path $out 'CxVisual.Mechanism.Data.dll')))
    & $csc @args $entry @sources
    if($LASTEXITCODE -ne 0){throw 'Config-only Roslyn test compile failed'}
    Write-Host 'PASS config-only: Foundation.Data + Mechanism.Data + shared cases, no Studio/Runtime'
} finally { Pop-Location }
