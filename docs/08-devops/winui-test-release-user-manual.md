# WinUI Test Release - User Prerequisites and Install Guide

## Purpose

This guide explains what end users need installed before running the unpackaged WinUI test release and how to start the app safely.

## Package Contents

Each test release zip/folder should include:

- `LabAssistant.exe`
- `install-and-run.ps1`
- `preflight-check.ps1`
- `TEST-RELEASE-README.txt`
- `WindowsAppRuntimeInstall-x64.exe` (optional but recommended to bundle)

## Required Prerequisites

1. Windows 10/11 x64 (build 19041 or newer)
2. .NET 8 Desktop Runtime (x64)
3. Windows App Runtime 1.8 (x64)
4. WebView2 Runtime (recommended)

Official links:

- .NET Desktop Runtime 8: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
- Windows App Runtime downloads: https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads
- WebView2 Runtime: https://developer.microsoft.com/microsoft-edge/webview2/

## Recommended Start Flow (End User)

1. Extract the full release zip to a local folder.
2. Open PowerShell as Administrator in that folder.
3. Run:

```powershell
.\install-and-run.ps1
```

What this does:

- validates prerequisites
- attempts Windows App Runtime install from bundled installer (if missing)
- revalidates
- launches `LabAssistant.exe` when ready

## Manual Fallback Flow

If the one-command launcher is unavailable:

```powershell
.\preflight-check.ps1
.\preflight-check.ps1 -Launch
```

## Troubleshooting

- If app does not start, check Windows Event Viewer -> Application log for `LabAssistant.exe` crash entries.
- If startup crash breadcrumbs exist, check:
  - `%LOCALAPPDATA%\LabAssistant\logs\startup-crash.log`
  - `%TEMP%\LabAssistant-startup-crash.log`
- If SmartScreen blocks launch, choose **More info -> Run anyway**.

## Open Questions / TBD

- TBD: production installer packaging path (MSIX/bootstrapper) for non-technical users.
