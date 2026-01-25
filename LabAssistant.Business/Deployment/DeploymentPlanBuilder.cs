using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Deployment;

public class DeploymentPlanBuilder
{
    private static readonly string[] BaseSteps =
    {
        "CheckHyperV",
        "CreateVmFolder",
        "CreateVhd",
        "CreateVm",
        "AddNic",
        "ConfigureVm",
        "EnableGuestServices",
        "DisableVmCheckpoints",
        "StartVm"
    };

    public DeploymentPlan Build(LabTemplate template)
    {
        var plan = new DeploymentPlan();

        foreach (var vm in template.VmTemplates)
        {
            foreach (var step in BaseSteps)
            {
                plan.Steps.Add(new DeploymentPlanStep
                {
                    VmName = vm.Name,
                    Name = step,
                    Details = $"{step} for {vm.Name}"
                });
            }
        }

        return plan;
    }
}
