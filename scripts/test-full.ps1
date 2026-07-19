param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\tests\PTGOilSystem.Web.Tests\PTGOilSystem.Web.Tests.csproj'
$assembly = Join-Path $PSScriptRoot '..\tests\PTGOilSystem.Web.Tests\bin\Debug\net8.0\PTGOilSystem.Web.Tests.dll'

if ($NoBuild) {
    if (-not (Test-Path -LiteralPath $assembly)) {
        throw 'No successful Debug test build exists. Run this script without -NoBuild first.'
    }
} else {
    dotnet build $project -c Debug --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

dotnet test $project -c Debug --no-build --no-restore
exit $LASTEXITCODE
