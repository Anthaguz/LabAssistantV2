using CommunityToolkit.Mvvm.ComponentModel;
using LabAssistant.Business.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

public partial class BaseDiskListItem : ObservableObject
{
    public BaseDiskListItem(AssetsBaseDiskRecord record)
    {
        Id = record.Id;
        Path = record.Path;
        OsName = record.OsName;
        OsVersion = record.OsVersion;
        Generation = record.Generation;
        Notes = record.Notes;
        BootstrapExpectedLocalUser = record.BootstrapExpectedLocalUser;
        BootstrapLocalCredentialSlotRef = record.BootstrapLocalCredentialSlotRef;
        BootstrapGuestOsFamily = record.BootstrapGuestOsFamily;
        BootstrapGuestTransport = record.BootstrapGuestTransport;
        BootstrapNotes = record.BootstrapNotes;
        SizeDisplay = record.SizeBytes.HasValue ? FormatSize(record.SizeBytes.Value) : "Size unavailable";
    }

    public string Id { get; }

    public string Path { get; }

    public string OsName { get; }

    public string OsVersion { get; }

    public int Generation { get; }

    public string? Notes { get; }

    public string? BootstrapExpectedLocalUser { get; }

    public string? BootstrapLocalCredentialSlotRef { get; }

    public string? BootstrapGuestOsFamily { get; }

    public string? BootstrapGuestTransport { get; }

    public string? BootstrapNotes { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(OsVersion)
        ? OsName
        : $"{OsName} {OsVersion}";

    public string SizeDisplay { get; }

    [ObservableProperty]
    private string _status = "Validation has not been evaluated.";

    [ObservableProperty]
    private string _referenceSummary = "No removal assessment has been performed.";

    private static string FormatSize(long sizeBytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = sizeBytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
