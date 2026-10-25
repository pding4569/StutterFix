$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $root
foreach ($editionName in @('dev','player','playerauto')) {
    $argsList = @('build','StutterFix.csproj','-v','q','--nologo',
        "-p:OutputPath=$root/out/imageown4/build/$editionName/",
        "-p:BaseIntermediateOutputPath=$root/out/imageown4/obj/$editionName/",
        "-p:RestoreConfigFile=$root/out/imageown4/NuGet.Config")
    if ($editionName -ne 'dev') { $argsList += '-p:Edition=Player' }
    if ($editionName -eq 'playerauto') { $argsList += '-p:AutoTestBuild=1' }
    & dotnet @argsList *> "out/imageown4/build-$editionName.txt"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $editionName" }
    $mode = '--bake'
    if ($editionName -eq 'player') { $mode = '--no-bake' }
    & dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll "out/imageown4/build/$editionName/StutterFix.dll" $mode *> "out/imageown4/loadcheck-$editionName.txt"
    if ($LASTEXITCODE -ne 0) { throw "LoadCheck failed: $editionName" }
    Write-Output "$editionName build/LoadCheck passed"
}
& py -3 out/imageown4/check-generator.py
if ($LASTEXITCODE -ne 0) { throw 'Generator tests failed' }
Write-Output 'Generator checks passed'
