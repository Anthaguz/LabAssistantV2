using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;
using System.Text;

public class PersistentPowerShellSession : IPersistentPowerShellSession
{
    private readonly Process _process;
    private readonly StreamWriter _input;
    private readonly StreamReader _output;
    private readonly StreamReader _error;

    public PersistentPowerShellSession()
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _error = _process.StandardError;
    }

    public async Task<(string Output, string Error)> ExecuteAsync(string command)
    {
        const string OutputMarker = "__END_OF_OUTPUT__";
        const string ErrorMarker = "__END_OF_ERROR__";

        await _input.WriteLineAsync(command);
        await _input.WriteLineAsync($"echo {OutputMarker}");
        await _input.WriteLineAsync($"[System.Console]::Error.WriteLine('{ErrorMarker}')");

        var output = new StringBuilder();
        var error = new StringBuilder();

        var outputTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await _output.ReadLineAsync()) != null)
            {
                if (line.Contains(OutputMarker))
                    break;
                output.AppendLine(line);
            }
        });

        var errorTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await _error.ReadLineAsync()) != null)
            {
                if (line.Contains(ErrorMarker))
                    break;
                error.AppendLine(line);
            }
        });

        await Task.WhenAll(outputTask, errorTask);

        string cleanedOutput = CleanLines(output.ToString());
        string cleanedError = CleanLines(error.ToString());
        return (cleanedOutput, cleanedError);
    }

    private static string CleanLines(string raw)
    {
        return raw
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                !line.StartsWith("PS ", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Install the latest", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("PS C:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .Aggregate(new StringBuilder(), (sb, line) => sb.AppendLine(line), sb => sb.ToString().Trim());
    }


    public void Dispose()
    {
        _input.WriteLine("exit");
        _process.WaitForExit();
        _process.Dispose();
    }
}