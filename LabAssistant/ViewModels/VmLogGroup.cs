using System.Collections.ObjectModel;

namespace LabAssistant.ViewModels;

public class VmLogGroup
{
    public Guid VmId { get; init; }
    public string VmName { get; set; } = string.Empty;
    public ObservableCollection<string> Entries { get; set; } = new();
}
