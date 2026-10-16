param(
    [string] $OutputPath = ""
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $root 'Output/Debug/x64/DependencyAudit.csv'
}
$projects = @(Get-ChildItem -Path $root -Recurse -Filter 'CxVisual*.csproj' -File |
    Where-Object { $_.FullName -notmatch '[\\/](Legacy|Output)[\\/]' })
$owners = @{}
$paths = @{}
$refs = @{}
$rows = New-Object System.Collections.Generic.List[object]
foreach ($project in $projects) {
    $path = [System.IO.Path]::GetFullPath($project.FullName)
    $relative = $path.Substring($root.Length).TrimStart('\','/')
    $segments = $relative -split '[\\/]'
    if ($segments.Count -lt 3) { throw "Invalid owner path: $relative" }
    $owners[$path] = $segments[0]
    $paths[$path] = $relative
    $refs[$path] = New-Object System.Collections.Generic.List[string]
}
foreach ($project in $projects) {
    $path = [System.IO.Path]::GetFullPath($project.FullName)
    [xml]$xml = Get-Content -LiteralPath $path -Raw
    foreach ($group in @($xml.Project.ItemGroup)) {
        foreach ($reference in @($group.ProjectReference)) {
            if ($null -eq $reference -or -not $reference.Include) { continue }
            $target = [System.IO.Path]::GetFullPath(
                (Join-Path $project.DirectoryName $reference.Include))
            if (-not $owners.ContainsKey($target)) {
                throw "ProjectReference points outside known solution: $($paths[$path]) => $target"
            }
            if ($refs[$path].Contains($target)) {
                throw "Duplicate reference: $($paths[$path]) => $($paths[$target])"
            }
            $refs[$path].Add($target)
            $sourceLayer = $owners[$path]
            $targetLayer = $owners[$target]
            if ($sourceLayer -eq 'Foundation' -and $targetLayer -ne 'Foundation') {
                throw "Foundation upward reference: $($paths[$path]) => $($paths[$target])"
            }
            if ($sourceLayer -eq 'Mechanism' -and
                @('Professional','Applications','Intelligence','Tests') -contains $targetLayer) {
                throw "Mechanism upward reference: $($paths[$path]) => $($paths[$target])"
            }
            if ($sourceLayer -ne 'Tests' -and $targetLayer -eq 'Tests') {
                throw "Production references test project: $($paths[$path]) => $($paths[$target])"
            }
            if ($sourceLayer -eq 'Foundation' -and
                @('CxVisual.Foundation.Core','CxVisual.Foundation.Geometry',
                  'CxVisual.Foundation.Numerics') -contains $project.BaseName -and
                $project.BaseName -ne 'CxVisual.Foundation.Infrastructure' -and
                $target -match '[\\/]Foundation[\\/]Infrastructure[\\/]') {
                throw "Unneeded default Infrastructure dependency: $($paths[$path])"
            }
            $rows.Add([pscustomobject]@{
                Project = $project.BaseName
                Owner = $sourceLayer
                ReferencedProject = [System.IO.Path]::GetFileNameWithoutExtension($target)
                ReferencedOwner = $targetLayer
                SourcePath = $paths[$path]
                TargetPath = $paths[$target]
            })
        }
    }
}
$state = @{}
$trail = New-Object System.Collections.Generic.List[string]
function Assert-NoDependencyCycle([string]$path) {
    $value = 0
    if ($state.ContainsKey($path)) { $value = $state[$path] }
    if ($value -eq 2) { return }
    if ($value -eq 1) {
        throw "Project dependency cycle: $([string]::Join(' -> ', $trail)) -> $($paths[$path])"
    }
    $state[$path] = 1
    $trail.Add($paths[$path])
    foreach ($next in $refs[$path]) { Assert-NoDependencyCycle $next }
    $trail.RemoveAt($trail.Count - 1)
    $state[$path] = 2
}
foreach ($path in @($owners.Keys)) { Assert-NoDependencyCycle $path }
$testProject = @($projects | Where-Object { $_.BaseName -eq 'CxVisual.TestCenter' })
if ($testProject.Count -ne 1) { throw "Exactly one TestCenter project required" }
$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
@($rows | Sort-Object Owner,Project,ReferencedProject) |
    Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
Write-Host "PASS: dependency DAG $($projects.Count) projects, $($rows.Count) references, zero invalid upward edges/cycles"
Write-Host "Dependency matrix: $OutputPath"
