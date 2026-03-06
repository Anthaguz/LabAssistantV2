using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;
using System.Collections.Generic;

namespace LabAssistant.Business.Deployment
{ 
    public abstract class GuestOsConfigurationStep : DeploymentStep
    {
        protected abstract string GuestStepKey { get; }
        protected abstract string GuestStepDisplayName { get; }
        protected abstract bool IsSelected(VmDeploymentContext context);
        protected virtual bool IsImplementedStep => true;

        protected override string StepKey => GuestStepKey;
        protected override string StepLabel => GuestStepDisplayName;

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            if (!IsImplementedStep)
            {
                RecordSkipped(context, GuestStepSkipReasons.NotImplemented, $"{GuestStepDisplayName} is not implemented yet.");
                return;
            }

            if (!context.GuestServicesEnabled)
            {
                RecordSkipped(context, GuestStepSkipReasons.PrerequisiteUnavailable, $"Skipped {GuestStepDisplayName} - Guest Services not enabled.");
                return;
            }

            if (!IsSelected(context))
            {
                RecordSkipped(context, GuestStepSkipReasons.NotSelected, $"Skipped {GuestStepDisplayName} (not selected).");
                return;
            }

            ExecuteGuestStep(context);
            context.RecordGuestStepOutcome(GuestStepKey, GuestStepDisplayName, GuestStepOutcomeResults.Executed, message: $"{GuestStepDisplayName} executed.");
        }

        protected abstract void ExecuteGuestStep(VmDeploymentContext context);

        private void RecordSkipped(VmDeploymentContext context, string skipReason, string message)
        {
            context.Logs.Add(message);
            DebugLogger.Log($"[GuestOsConfigurationStep] {message}");
            context.SetStepTerminalOverride(GuestStepKey, DeployStepState.Skipped, message);
            context.RecordGuestStepOutcome(GuestStepKey, GuestStepDisplayName, GuestStepOutcomeResults.Skipped, skipReason, message);
            EmitStepEvent(
                context,
                "StepSkipped",
                "info",
                "skipped",
                GuestStepKey,
                new Dictionary<string, object?>
                {
                    ["skipReason"] = skipReason,
                    ["message"] = message
                });
        }
    }
}
