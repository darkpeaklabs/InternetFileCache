dotnet restore

$packagePath = Join-Path $PSScriptRoot "package"
$solutionPath = Join-Path $packagePath "InternetFileCache.sln"
$projectPath = Join-Path $packagePath "InternetFileCache/InternetFileCache.csproj"

dotnet build $solutionPath --configuration Release --no-restore --no-incremental
dotnet test  $solutionPath --configuration Release --no-build
dotnet pack  $projectPath --configuration Release --no-build
