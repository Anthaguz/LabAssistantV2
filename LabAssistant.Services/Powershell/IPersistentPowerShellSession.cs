namespace LabAssistant.Services.PowerShell;

public interface IPersistentPowerShellSession : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the session is still safe to reuse. A session becomes unusable
    /// once it faults (for example a cancelled command tore down its runspace) or once its backing
    /// process exits. The pool consults this on return and checkout so a dead session is retired
    /// instead of being handed back out and failing the next command.
    /// </summary>
    /// <remarks>
    /// Provided as a default interface member reporting always-alive so pre-existing lightweight
    /// implementers (test doubles that only supply <see cref="ExecuteAsync(string)"/>) keep satisfying
    /// the contract. Process-backed implementations (for example <see cref="PersistentPowerShellSession"/>)
    /// override this member.
    /// </remarks>
    bool IsAlive => true;

    /// <summary>
    /// Executes a command in the persistent session.
    /// </summary>
    /// <param name="command">The PowerShell command text to execute.</param>
    /// <returns>The cleaned standard output and error text.</returns>
    Task<(string Output, string Error)> ExecuteAsync(string command);

    /// <summary>
    /// Executes a command in the persistent session, honoring a cancellation token so a long-running
    /// guest script can be interrupted at the next safe boundary.
    /// </summary>
    /// <remarks>
    /// Provided as a default interface method so that pre-existing implementers that only supply the
    /// single-argument overload continue to satisfy the contract. Implementations that can honor
    /// cancellation (for example <see cref="PersistentPowerShellSession"/>) override this member.
    /// </remarks>
    /// <param name="command">The PowerShell command text to execute.</param>
    /// <param name="cancellationToken">Cancels a long-running execution at the next safe boundary.</param>
    /// <returns>The cleaned standard output and error text.</returns>
    Task<(string Output, string Error)> ExecuteAsync(string command, CancellationToken cancellationToken)
        => ExecuteAsync(command);

    /// <summary>
    /// Executes a command in the persistent session, first injecting secret-bearing variables into the
    /// runspace out-of-band so their plaintext never appears in the caller-visible (and therefore loggable)
    /// command string. Injected variables are removed from the runspace after the command completes.
    /// </summary>
    /// <remarks>
    /// Provided as a default interface method for backward compatibility; the default ignores
    /// <paramref name="secureVariables"/> and delegates to <see cref="ExecuteAsync(string)"/>. Implementations
    /// that support out-of-band secret injection (for example <see cref="PersistentPowerShellSession"/>)
    /// override this member.
    /// </remarks>
    /// <param name="command">The PowerShell command text, which may reference the injected variables by name.</param>
    /// <param name="secureVariables">Variable name to plaintext value pairs to inject and then clear. May be null.</param>
    /// <param name="cancellationToken">Cancels a long-running execution at the next safe boundary.</param>
    /// <returns>The cleaned standard output and error text.</returns>
    Task<(string Output, string Error)> ExecuteAsync(
        string command,
        IReadOnlyDictionary<string, string>? secureVariables,
        CancellationToken cancellationToken)
        => ExecuteAsync(command, cancellationToken);
}