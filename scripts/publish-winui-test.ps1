param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "LabAssistant.sln"
$projectPath = Join-Path $repoRoot "LabAssistant.WinUI\LabAssistant.WinUI.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts\winui-test-release"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$publishDir = Join-Path $artifactsRoot $timestamp
$zipPath = Join-Path $artifactsRoot "LabAssistant-$timestamp-$Runtime.zip"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

if (-not $SkipBuild) {
    Write-Host "Building solution ($Configuration)..."
    dotnet build $solutionPath -c $Configuration -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed."
    }
}

Write-Host "Publishing WinUI app ($Configuration, $Runtime)..."
$publishArgs = @(
    "publish"
    $projectPath
    "-c", $Configuration
    "-r", $Runtime
    "--self-contained", "false"
    "-p:WindowsAppSDKSelfContained=false"
    "-p:WindowsAppSdkBootstrapInitialize=true"
    "-p:PublishSingleFile=false"
    "-p:PublishReadyToRun=false"
    "-m:1"
    "-o", $publishDir
)
if ($SkipBuild) {
    $publishArgs += "--no-build"
} else {
    # Build step above already restored dependencies.
    $publishArgs += "--no-restore"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed."
}

$windowsAppRuntimeInstaller = Join-Path $repoRoot "WindowsAppRuntimeInstall-x64.exe"
if (Test-Path $windowsAppRuntimeInstaller) {
    Copy-Item -Path $windowsAppRuntimeInstaller -Destination (Join-Path $publishDir "WindowsAppRuntimeInstall-x64.exe") -Force
}

$preflightScript = Join-Path $repoRoot "scripts\preflight-check.ps1"
if (Test-Path $preflightScript) {
    Copy-Item -Path $preflightScript -Destination (Join-Path $publishDir "preflight-check.ps1") -Force
}

$installAndRunScript = Join-Path $repoRoot "scripts\install-and-run.ps1"
if (Test-Path $installAndRunScript) {
    Copy-Item -Path $installAndRunScript -Destination (Join-Path $publishDir "install-and-run.ps1") -Force
}

$readmePath = Join-Path $publishDir "TEST-RELEASE-README.txt"
@"
LabAssistant Test Release

How to run:
1) Open PowerShell as Administrator.
2) Change directory to this folder.
3) Run one-command launcher: .\install-and-run.ps1
4) If needed, run manually:
   - .\preflight-check.ps1
   - .\preflight-check.ps1 -Launch

Host prerequisites:
- Windows 10/11 x64 (build 19041+)
- Hyper-V feature enabled (for VM operations)
- Local Administrator rights for Hyper-V operations
- .NET 8 Desktop Runtime (x64)
- Windows App Runtime 1.8 (x64, package version >= 8000.616.304.0)
- WebView2 Runtime recommended
- Official links:
  - .NET Desktop Runtime 8: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
  - Windows App Runtime: https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads
  - WebView2 Runtime: https://developer.microsoft.com/microsoft-edge/webview2/

Startup diagnostics:
- Startup crash breadcrumbs are written to:
  %LOCALAPPDATA%\LabAssistant\logs\startup-crash.log
- If LocalAppData is unavailable, fallback path:
  %TEMP%\LabAssistant-startup-crash.log

Notes:
- This is an unpackaged WinUI test release profile (framework-dependent bootstrap).
- If preflight fails on Windows App Runtime, run WindowsAppRuntimeInstall-x64.exe (if included), then rerun preflight.
- If blocked by SmartScreen, choose More info -> Run anyway.
"@ | Set-Content -Path $readmePath

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating zip: $zipPath"
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "Publish output: $publishDir"
Write-Host "Zip package:    $zipPath"
Write-Host "End-user first run command: .\\install-and-run.ps1"
Write-Host "Done."
