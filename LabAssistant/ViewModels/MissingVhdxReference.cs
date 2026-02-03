using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels;

public sealed class MissingVhdxReference
{
    public MissingVhdxReference(VmTemplate vm, string missingId)
    {
        Vm = vm;
        MissingId = missingId;
    }

    public VmTemplate Vm { get; }

    public string MissingId { get; }

    public string VmName => string.IsNullOrWhiteSpace(Vm.Name) ? "<unnamed VM>" : Vm.Name;
}
