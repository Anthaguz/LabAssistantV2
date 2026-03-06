param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipBuild
)

# Usage:
# .\publish-winui-test.ps1
# .\publish-winui-test.ps1 -Configuration Release -Runtime win-x64

& "$PSScriptRoot\scripts\publish-winui-test.ps1" `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -SkipBuild:$SkipBuild
