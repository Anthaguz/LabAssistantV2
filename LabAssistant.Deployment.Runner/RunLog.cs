namespace LabAssistant.Deployment.Runner;

/// <summary>
/// Tees run output to both the console and a log file next to the working directory, so an elevated console
/// window that closes on exit still leaves a durable transcript.
/// </summary>
internal sealed class RunLog : IDisposable
{
    private readonly StreamWriter? _file;

    public RunLog(string fileName = "harness-run.log")
    {
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            _file = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }
        catch
        {
            // Console-only logging is an acceptable fallback if the log file cannot be opened.
            _file = null;
        }
    }

    public void Line(string message)
    {
        var stamped = $"{DateTimeOffset.Now:HH:mm:ss} {message}";
        Console.WriteLine(stamped);
        _file?.WriteLine(stamped);
    }

    public void Dispose()
    {
        _file?.Flush();
        _file?.Dispose();
    }
}
