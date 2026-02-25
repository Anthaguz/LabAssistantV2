using System;
using System.Collections.Generic;
using LabAssistant.Models.PowerShell;

namespace LabAssistant.Models.Deployment
{
    public class VmDeploymentContext
    {
        public Guid VmId;
        //Vm Base information
        public string VmPath { get; set; } = string.Empty;
        public string VmName { get; set; } = string.Empty;
        public int MemoryMb { get; set; } = 2048; // Default to 2GB
        public int CpuCount { get; set; } = 2; // Default to 2 CPUs

        // VHD Information
        // VhdPath is the differencing disk path attached to the VM.
        public string VhdPath { get; set; } = string.Empty;
        // BaseVhdPath is the parent/base VHD path used to create the differencing disk.
        public string BaseVhdPath { get; set; } = string.Empty;
        public string VhdDifferencingParentPath
        {
            get => BaseVhdPath;
            set => BaseVhdPath = value;
        }
        public string? VhdxId { get; set; }
        public string? VhdxSignature { get; set; }

        //Network Configuration
        public string VirtualSwitchName { get; set; } = string.Empty;

        public bool IsSuccess { get; set; } = true;
        public bool PerVmFailFast { get; set; } = true;
        public bool GuestServicesEnabled { get; set; } = false;
        public bool ConfigureTimeZone { get; set; } = false;
        public bool InstallSoftware { get; set; } = false;
        public bool InstallRole { get; set; } = false;
        public bool ConfigureNetworkInformation { get; set; } = false;
        public List<string> NonBlockingOptionalSteps { get; set; } = new();

        // Resource tracking for cleanup orchestration
        public bool VmFolderCreated { get; set; }
        public bool DifferencingDiskCreated { get; set; }
        public bool VmRegistered { get; set; }
        public bool VmStarted { get; set; }
        public bool WasCancelled { get; set; }
        public string? FailureStepKey { get; private set; }
        public string? FailureMessage { get; private set; }
        public VmCleanupResult? CleanupResult { get; set; }

        // Logging and PowerShell
        public List<string> Logs { get; } = new();
        public PowerShellHandle? PowerShellHandle { get; set; }
        public Action<string>? LogCallback { get; set; }
        public Action<string, string, string?, IReadOnlyDictionary<string, object?>?>? StructuredEventEmitter { get; set; }
        public Action? OnBlockingFailure { get; set; }
        public Func<bool>? ShouldAbort { get; set; }

        public bool IsStepNonBlocking(string stepKey)
        {
            return NonBlockingOptionalSteps != null && NonBlockingOptionalSteps.Contains(stepKey);
        }

        public void MarkFailure(
            string stepKey,
            string? message = null,
            IReadOnlyDictionary<string, object?>? extraContext = null)
        {
            if (IsStepNonBlocking(stepKey))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Logs.Add(message);
                }
                StructuredEventEmitter?.Invoke(
                    "StepFailed",
                    "warn",
                    "non_blocking_failed",
                    BuildStepFailedContext(stepKey, message, extraContext));
                return;
            }

            IsSuccess = false;
            FailureStepKey = stepKey;
            FailureMessage = message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                Logs.Add(message);
            }
            StructuredEventEmitter?.Invoke(
                "StepFailed",
                "error",
                "failed",
                BuildStepFailedContext(stepKey, message, extraContext));
            OnBlockingFailure?.Invoke();
        }

        private static IReadOnlyDictionary<string, object?> BuildStepFailedContext(
            string stepKey,
            string? message,
            IReadOnlyDictionary<string, object?>? extraContext)
        {
            var payload = new Dictionary<string, object?>
            {
                ["stepKey"] = stepKey,
                ["errorMessage"] = message
            };

            if (extraContext != null)
            {
                foreach (var pair in extraContext)
                {
                    payload[pair.Key] = pair.Value;
                }
            }

            return payload;
        }

        public void MarkCancelled()
        {
            WasCancelled = true;
        }

        public void ResetForNewOperation()
        {
            IsSuccess = true;
            GuestServicesEnabled = false;
            VmFolderCreated = false;
            DifferencingDiskCreated = false;
            VmRegistered = false;
            VmStarted = false;
            WasCancelled = false;
            FailureStepKey = null;
            FailureMessage = null;
            CleanupResult = null;
            Logs.Clear();
            PowerShellHandle = null;
            StructuredEventEmitter = null;
        }
    }
}
