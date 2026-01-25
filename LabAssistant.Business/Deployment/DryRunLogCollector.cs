using System.Collections.Generic;

namespace LabAssistant.Business.Deployment;

public class DryRunLogCollector : IDryRunLogger
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;

    public void Log(string message)
    {
        _entries.Add(message);
    }
}
