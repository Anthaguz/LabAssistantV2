namespace LabAssistant.Business.Runtime;

/// <summary>
/// Builds the in-guest probe used by the runtime's guest-stabilization gate.
/// </summary>
/// <remarks>
/// A single successful PowerShell Direct hop only proves the guest is reachable at that instant; it does not
/// prove the guest is finished with the reboot-prone specialize/OOBE window. This probe reports the guest as
/// stable only when nothing indicates an imminent reboot or an in-progress setup pass, so the runtime can wait
/// for several consecutive stable reports before running mutating steps. It is intentionally read-only and
/// never throws for a "not stable yet" condition: it emits <c>STABLE</c> or <c>PENDING</c> so a not-yet-stable
/// guest is a normal probe result rather than a transport error.
/// </remarks>
internal static class GuestStabilizationProbeScriptBuilder
{
    public const string StableMarker = "STABLE";
    public const string PendingMarker = "PENDING";

    public static string Build()
    {
        return string.Join(
            Environment.NewLine,
            "$pending = $false",
            // Component Based Servicing queues a reboot after servicing operations complete.
            "if (Test-Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Component Based Servicing\\RebootPending') { $pending = $true }",
            // Windows Update flags a required reboot here.
            "if (Test-Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\RebootRequired') { $pending = $true }",
            // A queued file-rename means a reboot is pending to apply it.
            "$__laPfro = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue).PendingFileRenameOperations",
            "if ($__laPfro) { $pending = $true }",
            // Setup / specialize still running: the guest can still reboot on its own out from under us.
            "$__laSetup = Get-ItemProperty 'HKLM:\\SYSTEM\\Setup' -ErrorAction SilentlyContinue",
            "if ($__laSetup.SystemSetupInProgress -and $__laSetup.SystemSetupInProgress -ne 0) { $pending = $true }",
            "if ($__laSetup.OOBEInProgress -and $__laSetup.OOBEInProgress -ne 0) { $pending = $true }",
            // The image must have reached the completed state; anything else means sysprep passes are still running.
            "$__laImageState = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Setup\\State' -ErrorAction SilentlyContinue).ImageState",
            "if ($__laImageState -and $__laImageState -ne 'IMAGE_STATE_COMPLETE') { $pending = $true }",
            $"if ($pending) {{ Write-Output '{PendingMarker}' }} else {{ Write-Output '{StableMarker}' }}");
    }
}
