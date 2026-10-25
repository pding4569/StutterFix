$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $root
foreach ($editionName in @('dev','player','playerauto')) {
    $argsList = @('build','StutterFix.csproj','-c','Release','-v','q','--nologo',
        "-p:OutputPath=$root/out/imageown7/build/$editionName/",
        "-p:BaseIntermediateOutputPath=$root/out/imageown7/obj/$editionName/",
        "-p:RestoreConfigFile=$root/out/imageown7/NuGet.Config")
    if ($editionName -ne 'dev') { $argsList += '-p:Edition=Player' }
    if ($editionName -eq 'playerauto') { $argsList += '-p:AutoTestBuild=1' }
    & dotnet @argsList *> "out/imageown7/build-$editionName.txt"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $editionName" }
    $mode = '--bake'
    if ($editionName -eq 'player') { $mode = '--no-bake' }
    & dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll "out/imageown7/build/$editionName/StutterFix.dll" $mode *> "out/imageown7/loadcheck-$editionName.txt"
    if ($LASTEXITCODE -ne 0) { throw "LoadCheck failed: $editionName" }
    Write-Output "$editionName build/LoadCheck passed"
}

