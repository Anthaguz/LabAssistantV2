param(
    [string]$Configuration = "Debug"
)

# Usage: .\build-run-winui.ps1 [-Configuration Debug|Release]
# Builds the full solution, then launches the unpackaged LabAssistant.WinUI app.

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    $argsList = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$PSCommandPath`"",
        "-Configuration", "`"$Configuration`""
    )

    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $argsList
    exit
}

& "$PSScriptRoot\scripts\build-run-winui.ps1" -Configuration $Configuration
