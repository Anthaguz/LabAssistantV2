namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// What a configured router guest reports about its RRAS / routing / NAT state over PowerShell Direct.
/// Every field is a durable, locale-independent signal (feature install state, the invariant
/// <c>Forwarding='Enabled'</c> enum, and a netsh exit code) so the assertions do not depend on parsing
/// localized console text.
/// </summary>
public sealed record GuestRouterState(
    bool RemoteAccessInstalled,
    bool RoutingInstalled,
    int Ipv4ForwardingEnabledCount,
    bool NatInstalled)
{
    /// <summary>True once every router-tail artifact the guest can attest to is in place.</summary>
    public bool IsFullyConfigured =>
        RemoteAccessInstalled && RoutingInstalled && Ipv4ForwardingEnabledCount > 0 && NatInstalled;
}

/// <summary>
/// Host-side guest validator that authenticates into a running router VM over PowerShell Direct (VMBus)
/// and reads the actual RRAS / routing / NAT state the router-tail deploy steps are supposed to have
/// applied - the in-guest corroboration of the RRAS/NAT tail, alongside the app's own
/// <c>deploy.step.run.end</c> completion events (which <see cref="DeployStepLogProbe"/> asserts).
///
/// A router keeps its LOCAL Administrator account (it never joins a domain), so we authenticate as
/// ".\Administrator", exactly like <see cref="GuestNetworkProbe"/>. The password is read from the
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD environment variable INSIDE the spawned PowerShell child, so the
/// secret never appears in a script string, a process argument, or a harness log.
/// </summary>
public sealed class GuestRouterProbe
{
    /// <summary>
    /// Polls the router over PowerShell Direct until it reports a fully-configured RRAS/routing/NAT
    /// state (routing can still be settling right after the deploy reports done) or the timeout elapses,
    /// then returns the state. Returns null when the guest never answered at all (or the poll was
    /// aborted); when it answered but never reached fully-configured, returns the LAST snapshot so the
    /// caller can report exactly which router artifact is missing.
    ///
    /// <paramref name="abortIf"/> is checked before every attempt: when it returns true the poll stops
    /// immediately and returns null, so a rolled-back deploy fails fast instead of polling a deleted VM.
    /// </summary>
    public GuestRouterState? QueryRouterState(string vmName, TimeSpan timeout, Func<bool>? abortIf = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastError = "(none)";
        GuestRouterState? lastSnapshot = null;

        while (DateTime.UtcNow < deadline)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine($"GuestRouterProbe: aborting router-state poll on '{vmName}' - the deploy target is gone (rolled back).");
                return null;
            }

            var result = PowerShellRunner.Run(BuildScript(vmName), TimeSpan.FromSeconds(90));
            if (result.Success)
            {
                if (TryParse(result.StdOut, out GuestRouterState snapshot))
                {
                    lastSnapshot = snapshot;
                    if (snapshot.IsFullyConfigured)
                    {
                        return snapshot;
                    }

                    lastError = $"router answered but is not fully configured yet: RemoteAccess={snapshot.RemoteAccessInstalled}, " +
                                $"Routing={snapshot.RoutingInstalled}, ForwardingEnabled={snapshot.Ipv4ForwardingEnabledCount}, Nat={snapshot.NatInstalled}.";
                }
                else
                {
                    lastError = "PowerShell Direct succeeded but the router-state readout could not be parsed.";
                }
            }
            else
            {
                lastError = result.StdErr.Trim();
            }

            Thread.Sleep(15000);
        }

        Console.WriteLine($"GuestRouterProbe: router on '{vmName}' never reached a fully-configured state. Last state: {lastError}");
        // Non-null when the guest answered at least once (so the caller can report the partial state);
        // null when it never answered.
        return lastSnapshot;
    }

    internal static bool TryParse(string stdout, out GuestRouterState state)
    {
        state = new GuestRouterState(false, false, 0, false);

        string? remoteAccess = Extract(stdout, "REMOTEACCESS");
        string? routing = Extract(stdout, "ROUTING");
        string? forwarding = Extract(stdout, "FORWARDING");
        string? nat = Extract(stdout, "NATINSTALLED");

        // A readable readout must at least carry the feature-state lines; a missing marker means the
        // guest answered with something unexpected, so treat it as unparsed rather than as "not installed".
        if (remoteAccess is null || routing is null || forwarding is null || nat is null)
        {
            return false;
        }

        int forwardingCount = int.TryParse(forwarding, out int parsed) ? parsed : 0;
        state = new GuestRouterState(
            RemoteAccessInstalled: string.Equals(remoteAccess, "Installed", StringComparison.OrdinalIgnoreCase),
            RoutingInstalled: string.Equals(routing, "Installed", StringComparison.OrdinalIgnoreCase),
            Ipv4ForwardingEnabledCount: forwardingCount,
            NatInstalled: string.Equals(nat, "True", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static string? Extract(string stdout, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            stdout, $@"^{System.Text.RegularExpressions.Regex.Escape(key)}=(.*)$",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string BuildScript(string vmName)
    {
        string vm = vmName.Replace("'", "''");
        // The password is pulled from the environment INSIDE this child process; it is never
        // interpolated into the script text. Build the SecureString with core .NET types only
        // (ConvertTo-SecureString lives in Microsoft.PowerShell.Security, which does not reliably
        // autoload in a spawned -NoProfile host).
        return $@"
$p = $env:{GuestDirectoryProbe.PasswordEnvVar}
if ([string]::IsNullOrEmpty($p)) {{ Write-Error 'admin password env var is not set'; exit 3 }}
$sec = New-Object System.Security.SecureString
foreach ($ch in $p.ToCharArray()) {{ $sec.AppendChar($ch) }}
$sec.MakeReadOnly()
$cred = New-Object System.Management.Automation.PSCredential('.\Administrator', $sec)
$out = Invoke-Command -VMName '{vm}' -Credential $cred -ScriptBlock {{
    $ra = (Get-WindowsFeature RemoteAccess -ErrorAction SilentlyContinue).InstallState
    $rt = (Get-WindowsFeature Routing -ErrorAction SilentlyContinue).InstallState
    # Forwarding='Enabled' is an invariant enum value, so counting matching interfaces is locale-safe.
    $fwd = @(Get-NetIPInterface -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {{ $_.Forwarding -eq 'Enabled' }}).Count
    # 'show global' only succeeds (exit 0) when the IP NAT routing component is installed, so the exit
    # code is a locale-independent 'NAT is configured' signal - no console text is parsed.
    & netsh routing ip nat show global *> $null
    $natInstalled = ($LASTEXITCODE -eq 0)
    'REMOTEACCESS=' + $ra
    'ROUTING=' + $rt
    'FORWARDING=' + $fwd
    'NATINSTALLED=' + $natInstalled
}} -ErrorAction Stop
$out | ForEach-Object {{ $_ }}
";
    }
}
