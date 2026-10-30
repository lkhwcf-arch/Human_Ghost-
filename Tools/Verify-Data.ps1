[CmdletBinding()]
param([switch]$CompileUnity, [string]$UnityEditorPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'HumanGhostClientChecks'))
$workRoot = Join-Path $temporaryRoot ([Guid]::NewGuid().ToString('N'))
$exitCode = 1
Push-Location -LiteralPath $projectRoot
try {
    dotnet run --project Tools/CsvReader.Checks/Check.csproj --property:BaseIntermediateOutputPath="$workRoot\Checks\obj\" --property:MSBuildProjectExtensionsPath="$workRoot\Checks\obj\" --property:BaseOutputPath="$workRoot\Checks\bin\" --property:LangVersion=9.0
    if ($LASTEXITCODE -ne 0) { throw '데이터 기능 검사에 실패했습니다.' }
    if ($CompileUnity) {
        if ([string]::IsNullOrEmpty($UnityEditorPath)) {
            $version = ([IO.File]::ReadAllText((Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt')) -split "`n" | Where-Object { $_ -match '^m_EditorVersion: ' }) -replace '^m_EditorVersion: ', ''
            $UnityEditorPath = Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $version.Trim() + '\Editor\Unity.exe')
        }
        $managed = Join-Path (Split-Path -Parent $UnityEditorPath) 'Data\Managed\UnityEngine'
        $references = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File)
        $references += @(Get-ChildItem Library/PackageCache -Filter '*newtonsoft*' -Directory | ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'Runtime\Newtonsoft.Json.dll') })
        $items = @()
        foreach ($file in $references) {
            $items += '<Reference Include="' + $file.BaseName + '"><HintPath>' + [Security.SecurityElement]::Escape($file.FullName) + '</HintPath><Private>false</Private></Reference>'
        }
        $xml = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>9.0</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><TreatWarningsAsErrors>true</TreatWarningsAsErrors><AssemblyName>Assembly-CSharp</AssemblyName></PropertyGroup><ItemGroup><Compile Include="' + [Security.SecurityElement]::Escape((Join-Path $projectRoot 'Assets\3.Script\**\*.cs')) + '"/><Compile Include="' + [Security.SecurityElement]::Escape((Join-Path $projectRoot 'Assets\Editor\**\*.cs')) + '"/>' + ($items -join '') + '</ItemGroup></Project>'
        $compileRoot = Join-Path $workRoot 'UnityCompile'
        New-Item -ItemType Directory -Path $compileRoot -Force | Out-Null
        $project = Join-Path $compileRoot 'UnityCompile.csproj'
        [IO.File]::WriteAllText($project, $xml)
        dotnet build $project --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw '엔진 모듈 참조 컴파일에 실패했습니다.' }
        Write-Host '엔진 모듈을 참조한 코드 컴파일을 통과했습니다. 실제 플레이·빌드 확인은 별도로 필요합니다.'
    }
    $exitCode = 0
}
catch { Write-Host $_.Exception.Message -ForegroundColor Red }
finally {
    Pop-Location
    $resolved = [IO.Path]::GetFullPath($workRoot)
    if ($resolved.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
exit $exitCode
