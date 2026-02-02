param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "..\LabAssistant.sln"
$projectPath = Join-Path $PSScriptRoot "..\LabAssistant\LabAssistant.csproj"

Write-Host "Building $solutionPath ($Configuration)..."
dotnet build $solutionPath -c $Configuration

Write-Host "Running LabAssistant..."
dotnet run --project $projectPath -c $Configuration
