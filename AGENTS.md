# AGENTS

## Scope
This repo contains a Windows desktop app (WPF/WinUI style) plus supporting class libraries. Treat the UI as the top-level composition root.

## Projects
- LabAssistant (UI app, net8.0-windows)
- LabAssistant.Models (domain objects, enums, DTOs)
- LabAssistant.Data (repositories, persistence)
- LabAssistant.Services (external APIs, file services, configuration)
- LabAssistant.Business (business logic, validation, coordination)

## Architecture boundaries and dependency rules
Allowed dependencies (strict):
- LabAssistant (UI) -> LabAssistant.Business, LabAssistant.Models, LabAssistant.Services
- LabAssistant.Business -> LabAssistant.Data, LabAssistant.Models, LabAssistant.Services
- LabAssistant.Data -> LabAssistant.Models (if shared types are required)
- LabAssistant.Services -> LabAssistant.Models (if shared types are required)
- LabAssistant.Models -> no project dependencies

Disallowed dependencies:
- No project may reference the UI project (LabAssistant).
- LabAssistant.Models must not reference any other project.
- LabAssistant.Data and LabAssistant.Services must not reference LabAssistant.Business or LabAssistant (UI).
- Cross-references between LabAssistant.Data and LabAssistant.Services are not allowed.

## Build commands (canonical)
From repo root:
- dotnet restore LabAssistant.sln
- dotnet build LabAssistant.sln -c Debug
- dotnet build LabAssistant.sln -c Release

Note: LabAssistant targets net8.0-windows, so builds must run on Windows with .NET SDK 8 installed.
