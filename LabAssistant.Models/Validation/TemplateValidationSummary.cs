using System.Collections.Generic;

namespace LabAssistant.Models.Validation;

public sealed class TemplateValidationSummary
{
    public TemplateValidationSummary(IEnumerable<string> errors, IEnumerable<string> warnings)
    {
        Errors = new List<string>(errors);
        Warnings = new List<string>(warnings);
    }

    public List<string> Errors { get; }

    public List<string> Warnings { get; }
}
