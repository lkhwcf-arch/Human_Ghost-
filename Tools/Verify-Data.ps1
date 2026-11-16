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
        $inputAssembly = Join-Path $projectRoot 'Library/ScriptAssemblies/Unity.InputSystem.dll'
        if (-not (Test-Path -LiteralPath $inputAssembly)) { throw '입력 시스템 참조가 없습니다. Unity에서 패키지 복원과 스크립트 컴파일을 먼저 완료해주세요.' }
        $references += Get-Item -LiteralPath $inputAssembly
        $references += Get-Item -LiteralPath (Join-Path $projectRoot 'Library/ScriptAssemblies/UnityEngine.UI.dll')
        foreach ($assembly in @('Unity.Addressables.dll', 'Unity.ResourceManager.dll')) {
            $path = Join-Path $projectRoot ('Library/ScriptAssemblies/' + $assembly)
            if (-not (Test-Path -LiteralPath $path)) { throw 'Addressables 참조가 없습니다. Unity에서 패키지 복원·코드 반영을 완료해 주세요.' }
            $references += Get-Item -LiteralPath $path
        }
        if ($env:DefineConstants -match 'UNITY_EDITOR') {
            $references += Get-Item -LiteralPath (Join-Path $projectRoot 'Library/ScriptAssemblies/Unity.Addressables.Editor.dll')
            $references += Get-Item -LiteralPath (Join-Path $projectRoot 'Library/ScriptAssemblies/Unity.2D.Animation.Runtime.dll')
        }
        $items = @()
        foreach ($file in $references) {
            $items += '<Reference Include="' + $file.BaseName + '"><HintPath>' + [Security.SecurityElement]::Escape($file.FullName) + '</HintPath><Private>false</Private></Reference>'
        }
        $sources = '<Compile Include="' + [Security.SecurityElement]::Escape((Join-Path $projectRoot 'Assets\3.Script\**\*.cs')) + '"/>'
        # 편집기 전용 소스는 Unity와 동일하게 편집기 조건에서만 검사합니다.
        if ($env:DefineConstants -match 'UNITY_EDITOR') {
            $sources += '<Compile Include="' + [Security.SecurityElement]::Escape((Join-Path $projectRoot 'Assets\Editor\**\*.cs')) + '"/>'
        }
        $xml = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>9.0</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><TreatWarningsAsErrors>true</TreatWarningsAsErrors><AssemblyName>Assembly-CSharp</AssemblyName></PropertyGroup><ItemGroup>' + $sources + ($items -join '') + '</ItemGroup></Project>'
        $compileRoot = Join-Path $workRoot 'UnityCompile'
        New-Item -ItemType Directory -Path $compileRoot -Force | Out-Null
        $project = Join-Path $compileRoot 'UnityCompile.csproj'
        [IO.File]::WriteAllText($project, $xml)
        dotnet build $project --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw '엔진 모듈 참조 컴파일에 실패했습니다.' }
        # Unity 의존성을 실행하지 않고 DLL 메타데이터로 테스트 전용 코드의 포함 여부를 확인합니다.
        $includeTests = $env:DefineConstants -match 'UNITY_EDITOR|DEVELOPMENT_BUILD'
        $testTypes = @('ManualSaveHost', 'DemoResetHost', 'TitleSaveSelection', 'SaveSlotScreen', 'DataLoadViewer')
        $testMethods = @('StepBossTests', 'CaptureFeedbackQa', 'CloseBossTests', 'DrawAggroTests', 'ClearAggroTests', 'RunAggroTest')
        $stream = [IO.File]::OpenRead((Join-Path $compileRoot 'bin/Debug/net10.0/Assembly-CSharp.dll'))
        $pe = $null
        try {
            $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
            $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $typeNames = [Collections.Generic.HashSet[string]]::new()
            $methodNames = [Collections.Generic.HashSet[string]]::new()
            foreach ($handle in $metadata.TypeDefinitions) {
                $definition = $metadata.GetTypeDefinition($handle)
                [void]$typeNames.Add($metadata.GetString($definition.Name))
                if ($metadata.GetString($definition.Name) -eq 'GameplayPreview') {
                    foreach ($method in $definition.GetMethods()) {
                        [void]$methodNames.Add($metadata.GetString($metadata.GetMethodDefinition($method).Name))
                    }
                }
            }
            foreach ($name in $testTypes) {
                if ($typeNames.Contains($name) -ne $includeTests) { throw "테스트 기능의 빌드 포함 조건이 잘못됐습니다: $name" }
            }
            foreach ($name in $testMethods) {
                if ($methodNames.Contains($name) -ne $includeTests) { throw "개발 시험 메서드의 빌드 포함 조건이 잘못됐습니다: $name" }
            }
            Write-Host "DLL 테스트 기능 포함 검사 통과: 포함=$includeTests (타입 $($testTypes.Count)개·시험 메서드 $($testMethods.Count)개)"
        }
        finally {
            if ($null -ne $pe) { $pe.Dispose() }
            $stream.Dispose()
        }
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
