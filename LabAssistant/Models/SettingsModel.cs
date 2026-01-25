using System;
using System.Collections.Generic;
using System.IO;

namespace LabAssistant.Models
{
    public class SettingsModel
    {
        public List<string> TemplatePaths { get; set; } = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LabAssistant", "Templates")
        };

        public string LogsPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LabAssistant", "Logs");

        public List<TemplateVhdxSelection> TemplateSelections { get; set; } = new List<TemplateVhdxSelection>();
    }
}
