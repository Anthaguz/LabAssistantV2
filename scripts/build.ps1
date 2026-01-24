param(
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "..\LabAssistant.sln"

& dotnet build $solutionPath -c $Configuration
exit $LASTEXITCODE
