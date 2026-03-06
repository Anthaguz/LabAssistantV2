param(
    [switch]$Launch,
    [string]$AppDir
)

$ErrorActionPreference = "Stop"

function Write-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details
    )

    $status = if ($Passed) { "PASS" } else { "FAIL" }
    Write-Host ("[{0}] {1} - {2}" -f $status, $Name, $Details)
}

function Test-WindowsDesktopRuntime8 {
    $runtimes = & dotnet --list-runtimes 2>$null
    return [bool]($runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 8\.' } | Select-Object -First 1)
}

function Get-WindowsAppRuntime18Version {
    $packages = @(
        Get-AppxPackage -ErrorAction SilentlyContinue |
            Where-Object {
                $_.PackageFullName -like "Microsoft.WindowsAppRuntime.1.8_*" -or
                $_.Name -like "MicrosoftCorporationII.WinAppRuntime.Main.1.8*"
            }
    )

    if (-not $packages -or $packages.Count -eq 0) {
        return $null
    }

    return ($packages | Sort-Object Version -Descending | Select-Object -First 1).Version
}

function Test-WindowsAppRuntime18 {
    $minimum = [Version]"8000.616.304.0"
    $installed = Get-WindowsAppRuntime18Version
    if ($null -eq $installed) {
        return @{ Passed = $false; Details = "Missing (required >= $minimum)." }
    }

    $ok = ([Version]$installed) -ge $minimum
    return @{
        Passed = $ok
        Details = "Installed $installed (required >= $minimum)."
    }
}

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

    $cwd = (Get-Location).Path
    if ((Test-Path (Join-Path $cwd "LabAssistant.exe")) -or (Test-Path (Join-Path $cwd "LabAssistant.WinUI.exe"))) {
        return $cwd
    }

    $repoRoot = Resolve-Path (Join-Path $scriptDir "..")
    $artifactsRoot = Join-Path $repoRoot "artifacts\winui-test-release"
    if (Test-Path $artifactsRoot) {
        $latest = Get-ChildItem $artifactsRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($latest -and ((Test-Path (Join-Path $latest.FullName "LabAssistant.exe")) -or (Test-Path (Join-Path $latest.FullName "LabAssistant.WinUI.exe")))) {
            return $latest.FullName
        }
    }

    return $scriptDir
}

$publishRoot = Resolve-AppRoot -RequestedDir $AppDir
$primaryExe = Join-Path $publishRoot "LabAssistant.exe"
$legacyExe = Join-Path $publishRoot "LabAssistant.WinUI.exe"
$appExe = if (Test-Path $primaryExe) { $primaryExe } else { $legacyExe }
$warInstaller = Join-Path $publishRoot "WindowsAppRuntimeInstall-x64.exe"

$allPassed = $true

if (Test-Path $appExe) {
    Write-Check -Name "App executable" -Passed $true -Details "Found at $appExe"
}
else {
    Write-Check -Name "App executable" -Passed $false -Details "Missing ($appExe)."
    $allPassed = $false
}

$desktopRuntimePassed = Test-WindowsDesktopRuntime8
$desktopRuntimeDetails = if ($desktopRuntimePassed) { "Installed." } else { "Missing Microsoft.WindowsDesktop.App 8.x runtime." }
Write-Check -Name ".NET 8 Desktop Runtime" -Passed $desktopRuntimePassed -Details $desktopRuntimeDetails
if (-not $desktopRuntimePassed) {
    $allPassed = $false
}

$warResult = Test-WindowsAppRuntime18
Write-Check -Name "Windows App Runtime 1.8" -Passed $warResult.Passed -Details $warResult.Details
if (-not $warResult.Passed) {
    $allPassed = $false
}

if (-not $allPassed -and (Test-Path $warInstaller)) {
    Write-Host ""
    Write-Host "Windows App Runtime installer found in package:"
    Write-Host "  $warInstaller"
    Write-Host "Run it, then rerun this preflight."
}

if (-not $allPassed -and -not (Test-Path $appExe)) {
    Write-Host ""
    Write-Host "Tip: run this script from a publish folder, or pass -AppDir:"
    Write-Host "  .\\preflight-check.ps1 -AppDir C:\\path\\to\\publish -Launch"
}

if ($Launch) {
    if (-not $allPassed) {
        throw "Preflight failed. Fix prerequisites before launching."
    }

    Write-Host ""
    Write-Host "Launching $(Split-Path $appExe -Leaf) ..."
    Start-Process -FilePath $appExe -WorkingDirectory $publishRoot
}

if (-not $allPassed) {
    exit 1
}

exit 0
