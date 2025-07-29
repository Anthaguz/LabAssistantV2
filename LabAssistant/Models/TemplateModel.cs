using System.Collections.Generic;

namespace LabAssistant.Models
{
    public class TemplateModel
    {
        public string TemplateName { get; set; }
        public List<VmDefinition> Vms { get; set; }
    }

    public class VmDefinition
    {
        public string Name { get; set; }
        public string MasterImage { get; set; }
        public int CpuCount { get; set; }
        public int RamMB { get; set; }
        public string Role { get; set; }
        public bool DomainJoin { get; set; }
    }
}
