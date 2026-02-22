using System.Collections.Generic;
using LabAssistant.Models.Templates;

namespace LabAssistant.Models.Templates;

public class LabTemplateLoadResult
{
    public List<LabTemplate> Templates { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
