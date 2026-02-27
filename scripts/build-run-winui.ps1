param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "..\LabAssistant.sln"
$projectPath = Join-Path $PSScriptRoot "..\LabAssistant.WinUI\LabAssistant.WinUI.csproj"

Write-Host "Building $solutionPath ($Configuration)..."
dotnet build $solutionPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Build failed for configuration '$Configuration'."
}

Write-Host "Running LabAssistant.WinUI..."
dotnet run --project $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Run failed for LabAssistant.WinUI."
}
