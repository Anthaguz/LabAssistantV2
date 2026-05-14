using System.Diagnostics;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.HyperV;

/// <summary>
/// Runs isolated Hyper-V administrative commands so action failures stay scoped to the command that triggered them.
/// </summary>
public sealed class HyperVAdministrativeCommandExecutor : IHyperVAdministrativeCommandExecutor
{
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;

    public HyperVAdministrativeCommandExecutor(Func<IPersistentPowerShellSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public async Task<HyperVPowerShellExecutionResult> ExecuteAsync(
        string commandName,
        string script,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionCreationStopwatch = Stopwatch.StartNew();
        using var session = _sessionFactory();
        sessionCreationStopwatch.Stop();

        var commandStopwatch = Stopwatch.StartNew();
        var (output, error) = await session.ExecuteAsync(script).ConfigureAwait(false);
        commandStopwatch.Stop();

        HyperVPowerShellTimingLogger.LogAdministrativeCommand(
            commandName,
            sessionCreationStopwatch.ElapsedMilliseconds,
            commandStopwatch.ElapsedMilliseconds,
            string.IsNullOrWhiteSpace(error));

        return new HyperVPowerShellExecutionResult
        {
            Output = output,
            Error = error,
            SessionCreated = true,
            SessionCreationDurationMs = sessionCreationStopwatch.ElapsedMilliseconds,
            CommandDurationMs = commandStopwatch.ElapsedMilliseconds
        };
    }
}
