param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "..\LabAssistant.sln"

Write-Host "Running tests for $solutionPath ($Configuration)..."
dotnet test $solutionPath -c $Configuration
