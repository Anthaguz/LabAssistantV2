using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// The guest-command dispatcher dedicated to a single VM.
/// </summary>
/// <remarks>
/// Owns one dedicated guest-command session (an <see cref="IPersistentPowerShellSession"/> that is deliberately
/// NOT drawn from the shared catalog/admin pool, so guest work can never starve read queries). With the
/// one-shot PowerShell Direct model each guest step opens a fresh hop: reuse of a held-open
/// <c>New-PSSession -VMName</c> is impossible over a stdin-driven host, because the host must close stdin (EOF)
/// for PowerShell Direct to connect at all, which also ends any held session. See
/// <see cref="OneShotPowerShellDirectSession"/> for the full root cause.
///
/// Two invariants hold regardless of the session implementation:
/// - Serialization within a VM. The <see cref="_stepLock"/> ensures two guest steps for the same VM can never
///   overlap (for example a configuration step must not run while a prior step is rebooting the guest). Cross-VM
///   parallelism is preserved because each VM has its own instance and therefore its own lock.
/// - Reboot and identity shifts self-heal. Every step is a fresh PowerShell Direct hop that authenticates with
///   the caller-supplied credential, so a reboot (which would sever a held session) and a credential change
///   (local admin to domain admin across promotion) need no special-casing.
/// </remarks>
public sealed class VmGuestSession : IDisposable
{
    private const string GuestPasswordVariableName = "__laGuestPassword";

    private readonly string _vmName;
    private readonly IPersistentPowerShellSession _hostSession;
    private readonly SemaphoreSlim _stepLock = new(1, 1);
    private bool _disposed;

    public VmGuestSession(string vmName, IPersistentPowerShellSession hostSession)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vmName);
        ArgumentNullException.ThrowIfNull(hostSession);
        _vmName = vmName;
        _hostSession = hostSession;
    }

    /// <summary>
    /// Runs a guest script as a fresh PowerShell Direct hop. The password is supplied out-of-band as a secure
    /// runspace variable so it never appears in the emitted (and therefore loggable) command text.
    /// </summary>
    public async Task<(string Output, string Error)> ExecuteAsync(
        string username,
        string? password,
        string script,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var command = BuildDispatchCommand(_vmName, username, script);
        var secureVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GuestPasswordVariableName] = password ?? string.Empty
        };

        // Serialize this VM's guest steps so the "one guest step per VM at a time" invariant holds regardless of
        // the session implementation and is unit-testable without a real host process. Different VMs use
        // different instances, so this never blocks cross-VM parallelism.
        await _stepLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _hostSession.ExecuteAsync(command, secureVariables, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stepLock.Release();
        }
    }

    /// <summary>
    /// No-op retained for API compatibility. Every step is already a fresh PowerShell Direct hop, so there is
    /// no held in-guest connection to drop; a reboot self-heals automatically on the next step.
    /// </summary>
    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The one-shot guest session retains no process between calls, so disposing it is a no-op; the call is
        // kept so any future session implementation that does hold resources is torn down here.
        _hostSession.Dispose();
        _stepLock.Dispose();
    }

    /// <summary>
    /// Builds the host-side command that opens a fresh PowerShell Direct hop into the VM and runs the caller's
    /// script. No secret is interpolated: the plaintext password is injected separately as the
    /// <c>$__laGuestPassword</c> runspace variable, so the returned string is safe to log.
    /// </summary>
    /// <remarks>
    /// The credential's <see cref="System.Security.SecureString"/> is built with <c>AppendChar</c> rather than
    /// <c>ConvertTo-SecureString</c>: from a .NET-hosted <c>powershell.exe</c> child, <c>ConvertTo-SecureString</c>
    /// autoloads <c>Microsoft.PowerShell.Security</c>, which races the stdin-EOF teardown and can throw, nulling
    /// the password. <c>AppendChar</c> has no module dependency and is race-free.
    /// </remarks>
    internal static string BuildDispatchCommand(string vmName, string username, string script)
    {
        var quotedVmName = PowerShellCommandBuilder.Quote(vmName);
        var quotedUsername = PowerShellCommandBuilder.Quote(username);

        return string.Join(
            Environment.NewLine,
            "$guestSecurePassword = New-Object System.Security.SecureString",
            $"foreach ($__laPasswordChar in ${GuestPasswordVariableName}.ToCharArray()) {{ $guestSecurePassword.AppendChar($__laPasswordChar) }}",
            "$guestSecurePassword.MakeReadOnly()",
            $"$guestCredential = New-Object System.Management.Automation.PSCredential ({quotedUsername}, $guestSecurePassword)",
            "try {",
            // Fresh hop per step. Reuse of a held-open session is intentionally not attempted: the one-shot host
            // closes stdin (required so PowerShell Direct does not hang), which would end any held session anyway.
            $"  Invoke-Command -VMName {quotedVmName} -Credential $guestCredential -ErrorAction Stop -ScriptBlock {{",
            script,
            "  }",
            "}",
            "finally { Remove-Variable -Name 'guestSecurePassword','guestCredential','__laPasswordChar' -ErrorAction SilentlyContinue }");
    }
}
