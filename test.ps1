param(
    [string]$Configuration = "Debug"
)

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

& "$PSScriptRoot\scripts\test.ps1" -Configuration $Configuration
