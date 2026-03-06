param(
    [string]$AppDir
)

$ErrorActionPreference = "Stop"

function Resolve-AppRoot {
    param([string]$RequestedDir)

    if (-not [string]::IsNullOrWhiteSpace($RequestedDir) -and (Test-Path $RequestedDir)) {
        return (Resolve-Path $RequestedDir).Path
    }

    $scriptDir = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($scriptDir)) {
        $scriptDir = (Get-Location).Path
    }

    if ((Test-Path (Join-Path $scriptDir "LabAssistant.exe")) -or (Test-Path (Join-Path $scriptDir "LabAssistant.WinUI.exe"))) {
        return $scriptDir
    }

    return (Get-Location).Path
}

$publishRoot = Resolve-AppRoot -RequestedDir $AppDir
$preflightScript = Join-Path $publishRoot "preflight-check.ps1"
$windowsAppRuntimeInstaller = Join-Path $publishRoot "WindowsAppRuntimeInstall-x64.exe"

if (-not (Test-Path $preflightScript)) {
    throw "Missing preflight-check.ps1 in '$publishRoot'."
}

Write-Host "Running prerequisite check..."
& $preflightScript -AppDir $publishRoot
$preflightExit = $LASTEXITCODE

if ($preflightExit -eq 0) {
    Write-Host ""
    Write-Host "Prerequisites already satisfied."
    & $preflightScript -AppDir $publishRoot -Launch
    exit $LASTEXITCODE
}

if (Test-Path $windowsAppRuntimeInstaller) {
    Write-Host ""
    Write-Host "Attempting Windows App Runtime install..."
    Start-Process -FilePath $windowsAppRuntimeInstaller -ArgumentList "/install","/quiet","/norestart" -Wait -NoNewWindow
}
else {
    Write-Host ""
    Write-Host "Windows App Runtime installer not found in package."
}

Write-Host ""
Write-Host "Rechecking prerequisites..."
& $preflightScript -AppDir $publishRoot
$preflightExit = $LASTEXITCODE

if ($preflightExit -ne 0) {
    Write-Host ""
    Write-Host "Prerequisites are still missing."
    Write-Host "Install manually and rerun:"
    Write-Host "  Windows App Runtime: https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads"
    Write-Host "  .NET Desktop Runtime 8 (x64): https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
    exit 1
}

Write-Host ""
Write-Host "Launching application..."
& $preflightScript -AppDir $publishRoot -Launch
exit $LASTEXITCODE
