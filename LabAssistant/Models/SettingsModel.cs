using System.Collections.Generic;

namespace LabAssistant.Models
{
    public class SettingsModel
    {
        public List<string> TemplatePaths { get; set; } = new();

        public string LogsPath { get; set; } = string.Empty;
    }
}
