param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$src = Join-Path $root 'DLL\x64'
$out = Join-Path $root ('Output\' + $Configuration + '\x64')
New-Item -ItemType Directory -Force $out | Out-Null
if (Test-Path $src) {
    Get-ChildItem $src -File | Where-Object { $_.Extension -in @('.dll','.pdb','.config') } | ForEach-Object {
        Copy-Item $_.FullName -Destination $out -Force
    }
}
Write-Host 'Staged hand-managed x64 runtime binaries, if any. NuGet packages are not managed in DLL.'
