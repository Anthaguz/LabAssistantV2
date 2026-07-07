using System.Collections.Concurrent;
using LabAssistant.Business.Runtime;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

/// <summary>
/// Concurrency tests for <see cref="ConcurrentHashSet{T}"/>, the thread-safe set that backs the V2 runtime's
/// executed-node-id sink. These assert that many parallel writers never lose an item or throw, which a plain
/// <see cref="HashSet{T}"/> cannot guarantee under concurrent mutation.
/// </summary>
public sealed class ConcurrentHashSetTests
{
    [Fact]
    public async Task Add_ManyParallelWriters_LosesNothingAndDoesNotThrow()
    {
        const int writers = 32;
        const int perWriter = 500;
        var set = new ConcurrentHashSet<string>(StringComparer.Ordinal);
        using var start = new Barrier(writers);

        var tasks = Enumerable.Range(0, writers).Select(writer => Task.Run(() =>
        {
            // Release all writers at once to maximize contention on the shared set.
            start.SignalAndWait();
            for (var i = 0; i < perWriter; i++)
            {
                set.Add($"node-{writer}-{i}");
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(writers * perWriter, set.Count);
        Assert.Equal(writers * perWriter, set.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Add_ConcurrentDuplicateKeys_DeduplicatesAndReportsFirstWriterOnly()
    {
        const int writers = 16;
        const int distinctKeys = 250;
        var set = new ConcurrentHashSet<string>(StringComparer.Ordinal);
        var acceptedAdds = 0;
        using var start = new Barrier(writers);

        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < distinctKeys; i++)
            {
                if (set.Add($"shared-{i}"))
                {
                    Interlocked.Increment(ref acceptedAdds);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Every key is added by all writers, but only the winning Add per key returns true.
        Assert.Equal(distinctKeys, set.Count);
        Assert.Equal(distinctKeys, acceptedAdds);
    }
}
