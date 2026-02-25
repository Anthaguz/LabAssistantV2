using System.Reflection;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

[CollectionDefinition("DebugLogger tests", DisableParallelization = true)]
public sealed class DebugLoggerTestsCollectionDefinition
{
}

[Collection("DebugLogger tests")]
public class DebugLoggerTests
{
    private static readonly object DebugLoggerTestLock = new();

    [Fact]
    public void DebugLogger_Log_RotatesAndRetainsBoundedHistory()
    {
        lock (DebugLoggerTestLock)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"labassistant-debuglog-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                DebugLogger.SetLogFolder(directory);
                InvokeRotationHook("ConfigureRotationForTests", 1L, 2);

                for (var i = 0; i < 5; i++)
                {
                    DebugLogger.Log(new string((char)('a' + (i % 26)), 80));
                }

                DebugLogger.SetLogFolder(Path.Combine(Path.GetTempPath(), $"labassistant-debuglog-sink-{Guid.NewGuid():N}"));

                var active = Path.Combine(directory, DebugLoggingDefaults.DebugLogFileName);
                var rotated1 = Path.Combine(directory, "log.1.txt");
                var rotated2 = Path.Combine(directory, "log.2.txt");
                var rotated3 = Path.Combine(directory, "log.3.txt");

                Assert.True(File.Exists(active));
                Assert.True(File.Exists(rotated1));
                Assert.True(File.Exists(rotated2));
                Assert.False(File.Exists(rotated3));

                var activeContent = File.ReadAllText(active);
                Assert.Contains("[", activeContent, StringComparison.Ordinal);
                Assert.Contains("()", activeContent, StringComparison.Ordinal);
            }
            finally
            {
                InvokeResetHook();
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void DebugLogger_Log_ContinuesWritingToActiveFile_AfterRotation()
    {
        lock (DebugLoggerTestLock)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"labassistant-debuglog-active-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                DebugLogger.SetLogFolder(directory);
                InvokeRotationHook("ConfigureRotationForTests", 1L, 1);

                DebugLogger.Log(new string('x', 10));
                DebugLogger.Log("marker-after-rotation");

                DebugLogger.SetLogFolder(Path.Combine(Path.GetTempPath(), $"labassistant-debuglog-sink-{Guid.NewGuid():N}"));

                var active = Path.Combine(directory, DebugLoggingDefaults.DebugLogFileName);
                var rotated1 = Path.Combine(directory, "log.1.txt");

                Assert.True(File.Exists(active));
                Assert.True(File.Exists(rotated1));

                var activeContent = File.ReadAllText(active);
                var rotatedContent = File.ReadAllText(rotated1);

                Assert.Contains("marker-after-rotation", activeContent, StringComparison.Ordinal);
                Assert.DoesNotContain("marker-after-rotation", rotatedContent, StringComparison.Ordinal);
            }
            finally
            {
                InvokeResetHook();
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    private static void InvokeRotationHook(string methodName, long maxActiveFileBytes, int retainedHistoryFiles)
    {
        var method = typeof(DebugLogger).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, new object[] { maxActiveFileBytes, retainedHistoryFiles });
    }

    private static void InvokeResetHook()
    {
        var method = typeof(DebugLogger).GetMethod("ResetRotationForTests", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, Array.Empty<object>());
    }
}
