using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels
{
    public class TemplateEditorViewModel
    {
        public LabTemplate Template { get; }

        public ObservableCollection<VmTemplate> VmTemplates { get; }

        public TemplateEditorViewModel()
        {
            Template = new LabTemplate
            {
                Version = "v0"
            };

            VmTemplates = new ObservableCollection<VmTemplate>();
            VmTemplates.CollectionChanged += VmTemplates_CollectionChanged;
            SyncVmTemplates();
        }

        public void AddVm()
        {
            VmTemplates.Add(new VmTemplate
            {
                Name = "New VM",
                MemoryMb = 2048,
                CpuCount = 2
            });
        }

        public void RemoveVm(VmTemplate vmTemplate)
        {
            if (VmTemplates.Contains(vmTemplate))
            {
                VmTemplates.Remove(vmTemplate);
            }
        }

        private void VmTemplates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncVmTemplates();
        }

        private void SyncVmTemplates()
        {
            Template.VmTemplates = VmTemplates.ToList();
        }
    }
}
