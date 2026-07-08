using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// A persistent guest connection dedicated to a single VM.
/// </summary>
/// <remarks>
/// Owns one dedicated host runspace (an <see cref="IPersistentPowerShellSession"/> that is deliberately NOT
/// drawn from the shared catalog/admin pool, so guest work can never starve read queries) and, inside it, a
/// held-open in-guest runspace stored in the <c>$__laGuestSession</c> host variable. Every guest step for the
/// VM is dispatched through that one connection via <c>Invoke-Command -Session</c>, so the connection is
/// established once and reused instead of re-opening a fresh PowerShell Direct hop per step.
///
/// Two invariants fall out of using a single dedicated session:
/// - Serialization is inherent. The host session serializes its own <c>ExecuteAsync</c> calls, and each guest
///   step is a single self-contained command, so two guest steps for the same VM can never overlap. No extra
///   per-VM lock is required for correctness.
/// - Reboot and identity shifts self-heal. The dispatched command re-establishes <c>$__laGuestSession</c>
///   whenever it is missing, no longer <c>Opened</c> (a reboot severs it), or was opened for a different user
///   (the credential changes from local admin to domain admin across promotion). Callers therefore do not have
///   to special-case reboots.
/// </remarks>
public sealed class VmGuestSession : IDisposable
{
    private const string GuestPasswordVariableName = "__laGuestPassword";
    private const string GuestSessionVariableName = "__laGuestSession";
    private const string GuestSessionUserVariableName = "__laGuestSessionUser";

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
    /// Runs a guest script through the reused connection, establishing or re-establishing it first if needed.
    /// The password is supplied out-of-band as a secure runspace variable so it never appears in the emitted
    /// (and therefore loggable) command text.
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

        // Serialize this VM's guest steps here rather than relying on the injected session's internal locking,
        // so the "one guest step per VM at a time" invariant holds regardless of the session implementation and
        // is unit-testable without a real host process. Different VMs use different instances, so this never
        // blocks cross-VM parallelism.
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
    /// Drops the in-guest connection so the next <see cref="ExecuteAsync"/> re-establishes it. The dedicated
    /// host runspace stays alive. Safe to call when no connection exists.
    /// </summary>
    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _stepLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            await _hostSession.ExecuteAsync(BuildInvalidateCommand(), null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stepLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Disposing the dedicated host runspace tears down its child process, which also terminates the
        // in-guest connection it was holding, so no separate guest-side teardown is required.
        _hostSession.Dispose();
        _stepLock.Dispose();
    }

    /// <summary>
    /// Builds the host-side command that (re)establishes the reused guest connection when necessary and then
    /// dispatches the caller's script into the guest. No secret is interpolated: the plaintext password is
    /// injected separately as the <c>$__laGuestPassword</c> runspace variable, so the returned string is safe
    /// to log.
    /// </summary>
    internal static string BuildDispatchCommand(string vmName, string username, string script)
    {
        var quotedVmName = PowerShellCommandBuilder.Quote(vmName);
        var quotedUsername = PowerShellCommandBuilder.Quote(username);

        return string.Join(
            Environment.NewLine,
            $"$guestSecurePassword = ConvertTo-SecureString ${GuestPasswordVariableName} -AsPlainText -Force",
            $"$guestCredential = New-Object System.Management.Automation.PSCredential ({quotedUsername}, $guestSecurePassword)",
            "try {",
            // Re-establish when missing, severed (reboot), or opened for a different identity (local -> domain admin).
            $"  if ((-not ${GuestSessionVariableName}) -or (${GuestSessionVariableName}.State -ne 'Opened') -or (${GuestSessionUserVariableName} -ne {quotedUsername})) {{",
            $"    if (${GuestSessionVariableName}) {{ Remove-PSSession -Session ${GuestSessionVariableName} -ErrorAction SilentlyContinue }}",
            $"    ${GuestSessionVariableName} = New-PSSession -VMName {quotedVmName} -Credential $guestCredential -ErrorAction Stop",
            $"    ${GuestSessionUserVariableName} = {quotedUsername}",
            "  }",
            $"  Invoke-Command -Session ${GuestSessionVariableName} -ErrorAction Stop -ScriptBlock {{",
            script,
            "  }",
            "}",
            "finally { Remove-Variable -Name 'guestSecurePassword','guestCredential' -ErrorAction SilentlyContinue }");
    }

    internal static string BuildInvalidateCommand()
    {
        return string.Join(
            Environment.NewLine,
            $"if (${GuestSessionVariableName}) {{ Remove-PSSession -Session ${GuestSessionVariableName} -ErrorAction SilentlyContinue }}",
            $"${GuestSessionVariableName} = $null",
            $"${GuestSessionUserVariableName} = $null");
    }
}
