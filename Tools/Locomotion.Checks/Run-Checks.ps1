param([Parameter(Mandatory = $true)][string]$UnityEditorPath)
$ErrorActionPreference = 'Stop'

$editor = Split-Path -Parent (Resolve-Path -LiteralPath $UnityEditorPath).Path
$dotnet = Join-Path $editor 'Data/NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $editor 'Data/DotNetSdkRoslyn/csc.dll'
$mono = Join-Path $editor 'Data/MonoBleedingEdge/bin/mono.exe'
$references = Join-Path $editor 'Data/UnityReferenceAssemblies/unity-4.8-api'
foreach ($required in @($dotnet, $compiler, $mono, (Join-Path $references 'mscorlib.dll'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Unity 검사 도구 누락: $required" }
}
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('HumanGhostChecks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
$outputPath = Join-Path $temporaryDirectory 'LocomotionChecks.exe'
try {
    $sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.cs' | Select-Object -ExpandProperty FullName)
    foreach ($folder in @('Assets/3.Script/Core', 'Assets/3.Script/Data', 'Assets/3.Script/Common/Csv', 'Assets/3.Script/Common/Text')) {
        $recursive = $folder -eq 'Assets/3.Script/Core'
        $sources += Get-ChildItem -LiteralPath (Join-Path $project $folder) -File -Filter '*.cs' -Recurse:$recursive |
            Where-Object { $_.Directory.Name -ne 'Infrastructure' } | Select-Object -ExpandProperty FullName
    }
    $arguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:9', '/target:exe', "/out:$outputPath")
    foreach ($assembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
        $arguments += '/reference:' + (Join-Path $references $assembly)
    }
    & $dotnet $compiler @arguments @sources
    if ($LASTEXITCODE -ne 0) { throw '순수 코어 컴파일 실패' }
    & $mono $outputPath $project
    if ($LASTEXITCODE -ne 0) { throw '이동 코어 검사 실패' }
}
finally {
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath }
    Remove-Item -LiteralPath $temporaryDirectory
}
