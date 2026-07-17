using System.IO;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent unit tests for <see cref="DiagnosticsLogsViewModel"/>. They exercise the
/// filter projection (minimum level + facility include-set), the clear-filters reset, the resolved
/// Selected Context projections (registry message vs. legacy fallback), and the context-row parsing -
/// all against a fake <see cref="IStructuredLogViewerService"/>, with no WinUI dispatcher.
/// </summary>
public sealed class DiagnosticsLogsViewModelTests
{
    [Fact]
    public async Task InitializeAsync_LoadsEntries_AndReportsStatus()
    {
        var service = new FakeLogViewerService
        {
            Result = new StructuredLogViewerLoadResult
            {
                Entries = new[] { CodedEntry(), LegacyEntry() },
                TotalLineCount = 2
            }
        };
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        await vm.InitializeAsync();

        Assert.Equal(2, vm.Entries.Count);
        Assert.Contains("Loaded 2 events", vm.StatusText);
    }

    [Fact]
    public async Task ApplyFilters_ProjectsSelectedLevel_ToMinimumLevel()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        vm.SelectedLevelOption = vm.LevelOptions.Single(option => option.Rank == StatusLevelRank.Warn);
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Equal(StatusLevelRank.Warn, service.LastFilter!.MinimumLevel);
    }

    [Fact]
    public async Task ApplyFilters_PartialFacilitySelection_PassesOnlySelectedBytes()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        var first = vm.Facilities.First();
        first.IsSelected = true;
        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.NotNull(service.LastFilter!.Facilities);
        Assert.Equal(new[] { first.Value }, service.LastFilter!.Facilities!);
    }

    [Fact]
    public async Task ApplyFilters_AllFacilitiesSelected_PassesNullSet()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        foreach (var facility in vm.Facilities)
        {
            facility.IsSelected = true;
        }

        await vm.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Null(service.LastFilter!.Facilities);
    }

    [Fact]
    public void SelectingFacility_UpdatesFilterSummary()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        Assert.Equal("All facilities", vm.FacilityFilterSummary);

        vm.Facilities.First().IsSelected = true;

        Assert.Equal("1 selected", vm.FacilityFilterSummary);
    }

    [Fact]
    public async Task ClearFilters_ResetsLevelFacilitiesAndText()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        vm.OperationIdQuery = "op-123";
        vm.TextSearchQuery = "boom";
        vm.SelectedLevelOption = vm.LevelOptions.Single(option => option.Rank == StatusLevelRank.Error);
        vm.Facilities.First().IsSelected = true;

        await vm.ClearFiltersCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.OperationIdQuery);
        Assert.Equal(string.Empty, vm.TextSearchQuery);
        Assert.Null(vm.SelectedLevelOption.Rank);
        Assert.All(vm.Facilities, facility => Assert.False(facility.IsSelected));
        Assert.Equal("All facilities", vm.FacilityFilterSummary);
    }

    [Fact]
    public void SelectedEntry_CodedEntry_UsesRegistryMessageAndCode()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        vm.SelectedEntry = CodedEntry();

        Assert.True(vm.HasSelection);
        Assert.True(vm.HasCode);
        Assert.Equal("0x63030001", vm.SelectedCode);
        Assert.Equal("The virtual machine failed to start.", vm.SelectedMessage);
        Assert.True(vm.HasRemediation);
        Assert.True(vm.CopyCodeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedEntry_LegacyEntry_FallsBackToEventAndDisablesCopy()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        vm.SelectedEntry = LegacyEntry();

        Assert.False(vm.HasCode);
        Assert.False(vm.CopyCodeCommand.CanExecute(null));
        Assert.Equal("CleanupStarted (started)", vm.SelectedMessage);
    }

    [Fact]
    public void SelectedEntry_ParsesContextRows()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        vm.SelectedEntry = CodedEntry();

        Assert.Collection(
            vm.SelectedContextRows,
            row => { Assert.Equal("vmName", row.Key); Assert.Equal("dc01", row.Value); },
            row => { Assert.Equal("attempt", row.Key); Assert.Equal("3", row.Value); });
    }

    [Fact]
    public void SelectedEntry_ClearedSelection_EmptiesContextRows()
    {
        var service = new FakeLogViewerService();
        var vm = new DiagnosticsLogsViewModel(service, new FakeLauncher());

        vm.SelectedEntry = CodedEntry();
        Assert.NotEmpty(vm.SelectedContextRows);

        vm.SelectedEntry = null;

        Assert.Empty(vm.SelectedContextRows);
        Assert.False(vm.HasSelection);
    }

    private static StructuredLogViewerEntry CodedEntry() => new()
    {
        TimestampText = "2026-07-17 07:46:07.001",
        Level = "error",
        Event = "hyperv.vm-start.end",
        OperationId = "op-abc",
        Result = "failed",
        Code = "0x63030001",
        CodeValue = 0x63030001,
        FacilityByte = 0x03,
        Facility = "hyperv",
        Operation = "vm-start",
        Phase = "end",
        Severity = "Error",
        StatusByteText = "0x01",
        Title = "VM start failed",
        Message = "The virtual machine failed to start.",
        Remediation = "Check the Hyper-V event log and retry.",
        Thread = 12,
        Callsite = "V2RuntimeCapabilityService.cs:820",
        ContextJson = "{\"vmName\":\"dc01\",\"attempt\":3}",
        LevelRank = StatusLevelRank.Error
    };

    private static StructuredLogViewerEntry LegacyEntry() => new()
    {
        TimestampText = "2026-07-17 07:20:00.000",
        Level = "info",
        Event = "CleanupStarted",
        OperationId = "op-legacy",
        Result = "started",
        ContextJson = "{}",
        LevelRank = StatusLevelRank.Info
    };

    private sealed class FakeLogViewerService : IStructuredLogViewerService
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"la-log-test-{Guid.NewGuid():N}.jsonl");

        public FakeLogViewerService()
        {
            File.WriteAllText(_path, string.Empty);
        }

        public StructuredLogViewerLoadResult Result { get; set; } = new();

        public StructuredLogViewerFilter? LastFilter { get; private set; }

        public Task<StructuredLogViewerLoadResult> LoadAsync(StructuredLogViewerFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(Result);
        }

        public string GetStructuredLogFilePath() => _path;
    }

    private sealed class FakeLauncher : ILogLocationLauncher
    {
        public string? TryOpen(string filePath) => null;
    }
}
