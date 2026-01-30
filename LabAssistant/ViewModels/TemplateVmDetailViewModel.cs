using System.Collections.ObjectModel;
using System.ComponentModel;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels
{
    public class TemplateVmDetailViewModel : INotifyPropertyChanged
    {
        private readonly TemplateEditorViewModel _editorViewModel;

        public TemplateVmDetailViewModel(TemplateEditorViewModel editorViewModel, VmTemplate vmTemplate)
        {
            _editorViewModel = editorViewModel;
            VmTemplate = vmTemplate;
            _editorViewModel.PropertyChanged += EditorViewModelOnPropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public VmTemplate VmTemplate { get; }

        public ObservableCollection<string> AvailableSwitches => _editorViewModel.AvailableSwitches;

        public bool HasSwitches => _editorViewModel.HasSwitches;

        public bool ShowSwitchWarning => _editorViewModel.ShowSwitchWarning;

        public string SwitchWarning => _editorViewModel.SwitchWarning;

        private void EditorViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TemplateEditorViewModel.HasSwitches)
                or nameof(TemplateEditorViewModel.ShowSwitchWarning)
                or nameof(TemplateEditorViewModel.SwitchWarning))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }
    }
}
