using System;
using System.Collections.Generic;
using System.Threading;
using LabAssistant.Models.PowerShell;
using LabAssistant.Models.Templates;

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
        public List<string> VirtualSwitchNames { get; set; } = new();

        public bool IsSuccess { get; set; } = true;
        public bool PerVmFailFast { get; set; } = true;
        public bool GuestServicesEnabled { get; set; } = false;
        public bool ConfigureTimeZone { get; set; } = false;
        public bool InstallSoftware { get; set; } = false;
        public bool InstallRole { get; set; } = false;
        public bool ConfigureNetworkInformation { get; set; } = false;
        public TimeZoneStepConfig? TimeZoneConfig { get; set; }
        public SoftwareStepConfig? SoftwareConfig { get; set; }
        public RoleStepConfig? RoleConfig { get; set; }
        public GuestNetworkStepConfig? GuestNetworkConfig { get; set; }
        public List<string> NonBlockingOptionalSteps { get; set; } = new();
        public string? V2TopologyRole { get; set; }
        public string? V2BootstrapCredentialSlot { get; set; }
        public bool V2GuestTransportReady { get; set; }

        // Resource tracking for cleanup orchestration
        public bool VmFolderCreated { get; set; }
        public bool DifferencingDiskCreated { get; set; }
        public bool VmRegistered { get; set; }
        public bool VmStarted { get; set; }
        public bool WasCancelled { get; set; }
        public string? FailureStepKey { get; private set; }
        public string? FailureMessage { get; private set; }
        public IReadOnlyDictionary<string, object?>? FailureMetadata { get; private set; }
        public VmCleanupResult? CleanupResult { get; set; }
        public List<GuestStepExecutionOutcome> GuestStepOutcomes { get; } = new();

        // Logging and PowerShell
        public List<string> Logs { get; } = new();
        public PowerShellHandle? PowerShellHandle { get; set; }
        public Action<string>? LogCallback { get; set; }
        public Action<string, string, string?, IReadOnlyDictionary<string, object?>?>? StructuredEventEmitter { get; set; }
        public Action<DeployStepStateUpdate>? StepStateEmitter { get; set; }
        public Action? OnBlockingFailure { get; set; }
        public Func<bool>? ShouldAbort { get; set; }
        public string OperationId { get; set; } = string.Empty;

        private readonly Dictionary<string, (DeployStepState State, string? Message)> _stepTerminalOverrides = new(StringComparer.OrdinalIgnoreCase);
        private long _stepStateSequence;

        public bool IsStepNonBlocking(string stepKey)
        {
            return NonBlockingOptionalSteps != null && NonBlockingOptionalSteps.Contains(stepKey);
        }

        public void EmitStepState(
            string stepKey,
            string stepLabel,
            DeployStepState state,
            string? message = null)
        {
            if (StepStateEmitter is null || string.IsNullOrWhiteSpace(OperationId))
            {
                return;
            }

            StepStateEmitter.Invoke(new DeployStepStateUpdate(
                OperationId: OperationId,
                VmId: VmId,
                VmName: VmName,
                StepKey: stepKey,
                StepLabel: stepLabel,
                State: state,
                Message: message,
                TimestampUtc: DateTimeOffset.UtcNow,
                Sequence: Interlocked.Increment(ref _stepStateSequence)));
        }

        public void SetStepTerminalOverride(string stepKey, DeployStepState state, string? message = null)
        {
            _stepTerminalOverrides[stepKey] = (state, message);
        }

        public bool TryConsumeStepTerminalOverride(string stepKey, out DeployStepState state, out string? message)
        {
            if (_stepTerminalOverrides.TryGetValue(stepKey, out var value))
            {
                state = value.State;
                message = value.Message;
                _stepTerminalOverrides.Remove(stepKey);
                return true;
            }

            state = default;
            message = null;
            return false;
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
            FailureMetadata = BuildStepFailedContext(stepKey, message, extraContext);
            if (!string.IsNullOrWhiteSpace(message))
            {
                Logs.Add(message);
            }
            StructuredEventEmitter?.Invoke(
                "StepFailed",
                "error",
                "failed",
                FailureMetadata);
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

        public void RecordGuestStepOutcome(
            string stepKey,
            string displayName,
            string result,
            string? skipReason = null,
            string? message = null)
        {
            GuestStepOutcomes.Add(new GuestStepExecutionOutcome
            {
                StepKey = stepKey,
                DisplayName = displayName,
                Result = result,
                SkipReason = skipReason,
                Message = message
            });
        }

        public void ResetForNewOperation()
        {
            IsSuccess = true;
            GuestServicesEnabled = false;
            V2TopologyRole = null;
            V2BootstrapCredentialSlot = null;
            V2GuestTransportReady = false;
            VmFolderCreated = false;
            DifferencingDiskCreated = false;
            VmRegistered = false;
            VmStarted = false;
            WasCancelled = false;
            FailureStepKey = null;
            FailureMessage = null;
            FailureMetadata = null;
            CleanupResult = null;
            GuestStepOutcomes.Clear();
            Logs.Clear();
            PowerShellHandle = null;
            StructuredEventEmitter = null;
            StepStateEmitter = null;
            OperationId = string.Empty;
            _stepTerminalOverrides.Clear();
            Interlocked.Exchange(ref _stepStateSequence, 0);
        }
    }
}
