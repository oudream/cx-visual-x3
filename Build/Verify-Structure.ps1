$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$groups = @('Foundation','Mechanism','Professional','Applications','Intelligence','Tests')
# 仅统计正式解决方案所属六个顶层方面；Hello/AvalonDock等独立PoC不属于28个产品项目。
# 不通过全仓库递归枚举把示例验证工程误计为正式产品，仍严格验证Owner目录和引用。
$projects = @($groups | ForEach-Object {
    Get-ChildItem -Path (Join-Path $root $_) -Recurse -Filter 'CxVisual*.csproj' -File |
        Where-Object { $_.FullName -notmatch '[\\/](Legacy|Output)[\\/]' }
})
if ($projects.Count -ne 28) { throw "Expected 28 projects, got $($projects.Count)" }
foreach ($project in $projects) {
    $relativeDirectory = $project.DirectoryName.Substring($root.Length).TrimStart('\','/')
    $segments = $relativeDirectory -split '[\\/]'
    if ($segments.Count -ne 2 -or $groups -notcontains $segments[0] -or $segments[1].StartsWith('CxVisual.')) {
        throw "Project directory convention broken: $relativeDirectory"
    }
    [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw
    if ($xml.Project.PropertyGroup.TargetFrameworkVersion -notcontains 'v4.8') { throw "Not NET48: $($project.Name)" }
    # Traditional VS projects must declare configurations in the project itself.
    foreach ($configuration in @('Debug','Release')) {
        $condition = "'`$(Configuration)|`$(Platform)' == '$configuration|x64'"
        if (@($xml.Project.PropertyGroup | Where-Object { $_.Condition -eq $condition }).Count -ne 1) {
            throw "Missing or duplicate explicit $configuration|x64 configuration: $($project.FullName)"
        }
    }
    $assemblyName = @($xml.Project.PropertyGroup | ForEach-Object { $_.AssemblyName } | Where-Object { $_ })[0]
    if ($assemblyName -ne $project.BaseName) { throw "Assembly mismatch: $($project.FullName)" }
    foreach ($group in @($xml.Project.ItemGroup)) {
        foreach ($compile in @($group.Compile)) {
            if ($null -ne $compile -and $compile.Include) {
                $include = [string]$compile.Include
                if ($include.Contains('*') -or $include.Contains('?')) {
                    $wildcardRoot = ($include -split '[*?]')[0].TrimEnd('\','/')
                    $sourceRoot = Join-Path $project.DirectoryName $wildcardRoot
                    $matches = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse -File -ErrorAction SilentlyContinue)
                    if ($matches.Count -eq 0) { throw "No source files match Compile wildcard $include in $($project.FullName)" }
                } elseif (-not (Test-Path -LiteralPath (Join-Path $project.DirectoryName $include))) {
                    throw "Missing Compile $include in $($project.FullName)"
                }
            }
        }
        foreach ($reference in @($group.ProjectReference)) {
            if ($null -ne $reference -and $reference.Include -and -not (Test-Path -LiteralPath (Join-Path $project.DirectoryName $reference.Include))) {
                throw "Broken ProjectReference $($reference.Include) in $($project.FullName)"
            }
        }
    }
}
foreach ($slnName in @('CxVisual.Unified.sln','CxVisual.Foundation.sln')) {
    $slnPath = Join-Path $root $slnName
    $content = Get-Content -LiteralPath $slnPath
    foreach ($line in $content) {
        if ($line -match '^Project\("\{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC\}"\).*?, "(?<path>[^\"]+\.csproj)", "(?<guid>\{[^}]+\})"') {
            if (-not (Test-Path -LiteralPath (Join-Path $root $Matches['path']))) { throw "Broken solution project $($Matches['path'])" }
            $projectGuid = $Matches['guid']
            foreach ($configuration in @('Debug','Release')) {
                foreach ($kind in @('ActiveCfg','Build.0')) {
                    $mapping = "$projectGuid.$configuration|x64.$kind = $configuration|x64"
                    if (@($content | Where-Object { $_.Trim() -eq $mapping }).Count -ne 1) {
                        throw "Missing or duplicate build mapping in ${slnName}: $mapping"
                    }
                }
            }
        }
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $root '.agents'))) { throw '.agents folder missing' }
if (Test-Path -LiteralPath (Join-Path $root '.agents/IMPORT_R2_REQUIRED.md')) {
    Write-Warning 'Codex Skill V1.0 R2 original content has not been imported. Run Build/Import-CodexSkill-R2.ps1 when you have the ZIP.'
}
Write-Host 'PASS: 28 projects, NET48, explicit Debug/Release x64 configurations, solution build mappings, project references and paths.'
