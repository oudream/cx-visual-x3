param(
    [Parameter(Mandatory=$true)]
    [string]$SkillZip
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$zipPath = (Resolve-Path -LiteralPath $SkillZip).Path
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('cxvisual-codex-r2-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $tempRoot -Force
    $agentsFolders = @(Get-ChildItem -Path $tempRoot -Recurse -Directory -Force | Where-Object { $_.Name -eq '.agents' })
    if ($agentsFolders.Count -ne 1) {
        throw "Expected exactly one .agents folder inside Skill ZIP; found $($agentsFolders.Count)"
    }
    $source = $agentsFolders[0].FullName
    $files = @(Get-ChildItem -LiteralPath $source -File -Recurse -Force)
    if ($files.Count -eq 0) { throw 'Skill ZIP contains an empty .agents folder' }
    $destination = Join-Path $root '.agents'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($source.Length).TrimStart('\','/')
        $dst = Join-Path $destination $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $dst -Force
    }
    Remove-Item -LiteralPath (Join-Path $destination 'IMPORT_R2_REQUIRED.md') -ErrorAction SilentlyContinue
    Write-Host "Imported $($files.Count) Skill files into $destination" -ForegroundColor Green
} finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
