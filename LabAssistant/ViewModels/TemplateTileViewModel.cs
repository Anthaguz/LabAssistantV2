using System.Collections.Generic;
using System.Collections.ObjectModel;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels;

public class TemplateTileViewModel
{
    public TemplateTileViewModel(LabTemplate template, IEnumerable<string>? missingVhdxIds = null)
    {
        Id = template.Id;
        Name = template.Name;
        Description = template.Description ?? string.Empty;
        MissingVhdxIds = new ReadOnlyCollection<string>(
            missingVhdxIds != null ? new List<string>(missingVhdxIds) : new List<string>());
        IsReady = MissingVhdxIds.Count == 0;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsReady { get; }
    public IReadOnlyList<string> MissingVhdxIds { get; }
}
