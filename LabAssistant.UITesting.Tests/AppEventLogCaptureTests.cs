using System.Text;
using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Behaviour tests for the app-log slicing that backs deploy-failure evidence. The logic
/// runs entirely against files on disk (no app, no Hyper-V), so it is exercised here with
/// synthetic structured-events files in a temp folder.
/// </summary>
public sealed class AppEventLogCaptureTests : IDisposable
{
    private readonly string _appRoot;
    private readonly string _logsFolder;
    private readonly string _outDir;

    public AppEventLogCaptureTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-uitest-" + Guid.NewGuid().ToString("N"));
        _logsFolder = Path.Combine(_appRoot, "Logs");
        _outDir = Path.Combine(_appRoot, "out");
        Directory.CreateDirectory(_logsFolder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_appRoot, recursive: true);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    private AppEventLogCapture NewCapture() => new(new AppDataLocations(_appRoot));

    private static string Event(string ts, string operationId, string stepKey)
        => $"{{\"ts\":\"{ts}\",\"level\":\"info\",\"event\":\"StepCompleted\"," +
           $"\"operationId\":\"{operationId}\",\"context\":{{\"stepKey\":\"{stepKey}\"}}}}";

    private void WriteLog(string fileName, IEnumerable<string> lines)
        => File.WriteAllLines(Path.Combine(_logsFolder, fileName), lines);

    private static AppLogWindow WindowAt(string utc)
        => new(DateTimeOffset.Parse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind));

    private string Dest(string name) => Path.Combine(_outDir, name);

    [Fact]
    public void WriteSlice_excludes_events_before_window_and_sorts_ascending()
    {
        WriteLog("structured-events.jsonl", new[]
        {
            Event("2026-07-27T10:00:00.0000000Z", "op1", "before"),   // before window
            Event("2026-07-27T10:05:03.0000000Z", "op1", "later"),
            Event("2026-07-27T10:05:01.0000000Z", "op1", "earlier"),
        });

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:05:00.0000000Z"), null, dest);

        Assert.Equal(2, written);
        string[] lines = File.ReadAllLines(dest);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"stepKey\":\"earlier\"", lines[0]);
        Assert.Contains("\"stepKey\":\"later\"", lines[1]);
    }

    [Fact]
    public void WriteSlice_filters_to_a_single_operationId_when_supplied()
    {
        WriteLog("structured-events.jsonl", new[]
        {
            Event("2026-07-27T10:05:01.0000000Z", "op1", "mine"),
            Event("2026-07-27T10:05:02.0000000Z", "op2", "other"),
            Event("2026-07-27T10:05:03.0000000Z", "op1", "mine2"),
        });

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:05:00.0000000Z"), "op1", dest);

        Assert.Equal(2, written);
        string[] lines = File.ReadAllLines(dest);
        Assert.All(lines, l => Assert.Contains("\"operationId\":\"op1\"", l));
    }

    [Fact]
    public void WriteSlice_skips_malformed_and_timestampless_lines()
    {
        WriteLog("structured-events.jsonl", new[]
        {
            "this is not json",
            "{\"level\":\"info\",\"operationId\":\"op1\"}", // no ts
            Event("2026-07-27T10:05:01.0000000Z", "op1", "good"),
            "",
        });

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:05:00.0000000Z"), null, dest);

        Assert.Equal(1, written);
        Assert.Contains("\"stepKey\":\"good\"", File.ReadAllLines(dest).Single());
    }

    [Fact]
    public void WriteSlice_merges_rotated_files_and_orders_by_timestamp()
    {
        // Simulate a rotation mid-window: older events live in a history file, newer in the active file.
        WriteLog("structured-events-1.jsonl", new[]
        {
            Event("2026-07-27T10:05:01.0000000Z", "op1", "rotated-old"),
        });
        WriteLog("structured-events.jsonl", new[]
        {
            Event("2026-07-27T10:05:09.0000000Z", "op1", "active-new"),
        });

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:05:00.0000000Z"), null, dest);

        Assert.Equal(2, written);
        string[] lines = File.ReadAllLines(dest);
        Assert.Contains("\"stepKey\":\"rotated-old\"", lines[0]);
        Assert.Contains("\"stepKey\":\"active-new\"", lines[1]);
    }

    [Fact]
    public void WriteSlice_caps_to_newest_maxLines()
    {
        var lines = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            // Ascending seconds so ordering is deterministic.
            lines.Add(Event($"2026-07-27T10:05:{i:D2}.0000000Z", "op1", $"e{i}"));
        }

        WriteLog("structured-events.jsonl", lines);

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:00:00.0000000Z"), null, dest, maxLines: 3);

        Assert.Equal(3, written);
        string[] result = File.ReadAllLines(dest);
        // Keeps the three newest events, still in ascending order.
        Assert.Contains("\"stepKey\":\"e7\"", result[0]);
        Assert.Contains("\"stepKey\":\"e8\"", result[1]);
        Assert.Contains("\"stepKey\":\"e9\"", result[2]);
    }

    [Fact]
    public void WriteSlice_preserves_read_order_for_events_sharing_a_timestamp()
    {
        // Coarse clock granularity means a burst of events within one step commonly shares
        // an identical ts; the slice must keep them in the order they were logged.
        WriteLog("structured-events.jsonl", new[]
        {
            Event("2026-07-27T10:05:01.0000000Z", "op1", "first"),
            Event("2026-07-27T10:05:01.0000000Z", "op1", "second"),
            Event("2026-07-27T10:05:01.0000000Z", "op1", "third"),
        });

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:05:00.0000000Z"), null, dest);

        Assert.Equal(3, written);
        string[] lines = File.ReadAllLines(dest);
        Assert.Contains("\"stepKey\":\"first\"", lines[0]);
        Assert.Contains("\"stepKey\":\"second\"", lines[1]);
        Assert.Contains("\"stepKey\":\"third\"", lines[2]);
    }

    [Fact]
    public void WriteSlice_writes_nothing_when_no_events_match()
    {
        WriteLog("structured-events.jsonl", new[]
        {
            Event("2026-07-27T09:00:00.0000000Z", "op1", "old"),
        });

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:00:00.0000000Z"), null, dest);

        Assert.Equal(0, written);
        Assert.False(File.Exists(dest));
    }

    [Fact]
    public void WriteSlice_returns_zero_when_log_folder_is_missing()
    {
        Directory.Delete(_logsFolder, recursive: true);

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:00:00.0000000Z"), null, dest);

        Assert.Equal(0, written);
        Assert.False(File.Exists(dest));
    }

    [Fact]
    public void WriteSlice_reads_a_file_held_open_for_writing()
    {
        string path = Path.Combine(_logsFolder, "structured-events.jsonl");
        using var writer = new StreamWriter(
            new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(false));
        writer.WriteLine(Event("2026-07-27T10:05:01.0000000Z", "op1", "live"));
        writer.Flush();

        string dest = Dest("slice.jsonl");
        int written = NewCapture().WriteSlice(WindowAt("2026-07-27T10:05:00.0000000Z"), null, dest);

        Assert.Equal(1, written);
        Assert.Contains("\"stepKey\":\"live\"", File.ReadAllLines(dest).Single());
    }
}
