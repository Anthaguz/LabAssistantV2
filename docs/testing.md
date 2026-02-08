# Testing

Use these commands from the repo root.

## Build
- dotnet restore LabAssistant.sln
- dotnet build LabAssistant.sln -c Release

## Tests (all projects)
- dotnet test LabAssistant.sln -c Release

## Convenience scripts (Windows)
- .\test.ps1 (wraps scripts\test.ps1)

Notes:
- Tests are designed to run without elevated permissions.
- UI wiring tests target net8.0-windows and run on Windows agents.
- NETSDK1206 is suppressed for projects that reference System.Management.Automation (transitive Microsoft.Management.Infrastructure.Runtime.Win).
