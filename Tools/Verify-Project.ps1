[CmdletBinding()]
param([switch]$CompileUnity, [string]$UnityEditorPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) 'HumanGhostProjectChecks'
$workRoot = Join-Path $temporaryRoot ([Guid]::NewGuid().ToString('N'))
$shellPath = (Get-Process -Id $PID).Path
$stage = '데이터 검사'
$exitCode = 1
Push-Location -LiteralPath $projectRoot
try {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Verify-Data.ps1'))
    if ($CompileUnity) { $arguments += '-CompileUnity' }
    if ($UnityEditorPath) { $arguments += @('-UnityEditorPath', $UnityEditorPath) }
    Write-Host '데이터 검사와 선택한 엔진 참조 컴파일을 시작합니다.'
    & $shellPath @arguments
    if ($LASTEXITCODE -ne 0) { throw '데이터 검사 또는 엔진 참조 컴파일에 실패했습니다.' }
    $stage = '게임 코어 검사'
    Write-Host '게임 코어 검사를 시작합니다.'
    dotnet run --project Tools/Locomotion.Checks/Check.csproj --property:BaseIntermediateOutputPath="$workRoot/obj/" --property:MSBuildProjectExtensionsPath="$workRoot/obj/" --property:BaseOutputPath="$workRoot/bin/" -- $projectRoot
    if ($LASTEXITCODE -ne 0) { throw '게임 코어 검사에 실패했습니다.' }
    Write-Host '프로젝트 검사를 모두 통과했습니다. 실제 Unity 플레이와 플레이어 빌드는 별도 확인 대상입니다.'
    $exitCode = 0
}
catch { Write-Host "$stage 단계 실패: $($_.Exception.Message)" -ForegroundColor Red }
finally {
    Pop-Location
    $resolved = [IO.Path]::GetFullPath($workRoot)
    $boundary = [IO.Path]::GetFullPath($temporaryRoot) + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
exit $exitCode
