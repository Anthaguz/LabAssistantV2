using System.Text.Json;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.Data.Templates;

public class LabTemplateStore : ILabTemplateStore
{
    private static readonly Version V1SchemaVersion = TemplateSchemaVersionCatalog.V1Schema;
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<string> LastLoadWarnings { get; private set; } = Array.Empty<string>();

    public LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems)
    {
        var result = new LabTemplateLoadResult();
        LastLoadWarnings = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(templatesFolder))
        {
            result.Errors.Add("Templates folder is required.");
            return result;
        }

        if (!Directory.Exists(templatesFolder))
        {
            result.Errors.Add($"Templates folder not found: {templatesFolder}");
            return result;
        }

        var files = Directory.GetFiles(templatesFolder, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            result.Errors.Add("No template files found.");
            return result;
        }

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var template = JsonSerializer.Deserialize<LabTemplate>(json, LoadOptions);

                if (template == null)
                {
                    result.Errors.Add($"Template file did not parse: {file}");
                    continue;
                }

                var isLegacyV0 = IsLegacyV0Json(json);
                if (!TryNormalizeLoadedTemplate(template, isLegacyV0, out var normalized, out var warnings, out var loadError))
                {
                    result.Errors.Add($"{file}: {loadError}");
                    continue;
                }

                foreach (var warning in warnings)
                {
                    result.Warnings.Add($"{file}: {warning}");
                }

                normalized.ExecutionEngine = TemplateSchemaVersionCatalog.Classify(normalized.SchemaVersion, isLegacyV0);
                var validation = LabTemplateValidator.Validate(normalized, catalogItems);
                if (!validation.IsValid)
                {
                    foreach (var error in validation.Errors)
                    {
                        result.Errors.Add($"{file}: {error}");
                    }
                }

                if (validation.MissingVhdxIds.Count > 0)
                {
                    foreach (var missingId in validation.MissingVhdxIds)
                    {
                        result.Errors.Add($"{file}: missing VHDX reference '{missingId}'.");
                    }
                }

                result.Templates.Add(normalized);
            }
            catch (JsonException ex)
            {
                result.Errors.Add($"Template JSON parse error ({file}): {ex.Message}");
            }
            catch (IOException ex)
            {
                result.Errors.Add($"Template file read error ({file}): {ex.Message}");
            }
        }

        LastLoadWarnings = result.Warnings.ToList();
        return result;
    }

    public LabTemplate LoadFromFile(string filePath)
    {
        LastLoadWarnings = Array.Empty<string>();
        var json = File.ReadAllText(filePath);
        var template = JsonSerializer.Deserialize<LabTemplate>(json, LoadOptions);
        if (template == null)
        {
            throw new InvalidOperationException("Template file could not be loaded.");
        }

        var isLegacyV0 = IsLegacyV0Json(json);
        if (!TryNormalizeLoadedTemplate(template, isLegacyV0, out var normalized, out var warnings, out var error))
        {
            throw new InvalidOperationException(error);
        }

        normalized.ExecutionEngine = TemplateSchemaVersionCatalog.Classify(normalized.SchemaVersion, isLegacyV0);
        LastLoadWarnings = warnings.ToList();
        return normalized;
    }

    public void SaveToFile(string filePath, LabTemplate template)
    {
        template = NormalizeForSave(template);
        var json = JsonSerializer.Serialize(template, SaveOptions);
        File.WriteAllText(filePath, json);
    }

    public string SaveToFolder(string folderPath, string templateName, LabTemplate template)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Template folder path is required.", nameof(folderPath));
        }

        Directory.CreateDirectory(folderPath);
        var fileName = GetTemplateFileName(templateName, folderPath);
        var filePath = Path.Combine(folderPath, fileName);
        SaveToFile(filePath, template);
        return filePath;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "lab-template" : sanitized;
    }

    private static string GetTemplateFileName(string templateName, string folderPath)
    {
        var baseName = SanitizeFileName(templateName);
        var fileName = $"{baseName}.json";
        var filePath = Path.Combine(folderPath, fileName);
        var suffix = 1;

        while (File.Exists(filePath))
        {
            fileName = $"{baseName}-{suffix}.json";
            filePath = Path.Combine(folderPath, fileName);
            suffix++;
        }

        return fileName;
    }

    private static bool TryNormalizeLoadedTemplate(
        LabTemplate template,
        bool isLegacyV0,
        out LabTemplate normalized,
        out List<string> warnings,
        out string error)
    {
        warnings = new List<string>();
        error = string.Empty;
        var executionEngine = TemplateSchemaVersionCatalog.Classify(template.SchemaVersion, isLegacyV0);

        if (executionEngine == TemplateExecutionEngine.V2UnifiedPlanning)
        {
            if (!Version.TryParse(template.SchemaVersion, out var v2Version))
            {
                normalized = template;
                error = $"Template schemaVersion '{template.SchemaVersion}' is invalid. Use semantic version format like '{TemplateSchemaVersionCatalog.V2SchemaVersion}'.";
                return false;
            }

            if (v2Version.Major > TemplateSchemaVersionCatalog.V2Schema.Major)
            {
                normalized = template;
                error = $"Template schemaVersion '{v2Version}' is newer than this LabAssistant version supports. Please update LabAssistant.";
                return false;
            }

            if (v2Version.Major < TemplateSchemaVersionCatalog.V2Schema.Major)
            {
                normalized = template;
                error = $"Template schemaVersion '{v2Version}' is not a supported V2 major. Open and resave it through the intended compatibility path first.";
                return false;
            }

            if (v2Version > TemplateSchemaVersionCatalog.V2Schema)
            {
                warnings.Add($"Template schemaVersion '{v2Version}' is newer minor/patch than supported '{TemplateSchemaVersionCatalog.V2Schema}'. Continuing with compatible fields only.");
            }

            normalized = NormalizeForSave(template);
            normalized.ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning;
            return true;
        }

        var compatibility = TemplateSchemaCompatibilityGate.Evaluate(
            template.SchemaVersion,
            V1SchemaVersion,
            isLegacyV0);

        if (compatibility.IsBlocked)
        {
            error = compatibility.Error ?? "Template schemaVersion is not supported.";
            normalized = template;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(compatibility.Warning))
        {
            warnings.Add(compatibility.Warning);
        }

        normalized = compatibility.Status == TemplateSchemaCompatibilityStatus.AllowWithUpcast
            ? UpcastToCurrent(template)
            : template;

        normalized = NormalizeForSave(normalized);
        normalized.ExecutionEngine = TemplateExecutionEngine.V1Deployment;
        return true;
    }

    private static LabTemplate NormalizeForSave(LabTemplate source)
    {
        var normalized = source;
        var executionEngine = source.ExecutionEngine != default
            ? source.ExecutionEngine
            : TemplateSchemaVersionCatalog.Classify(source.SchemaVersion);

        // Save/export emits the canonical schema version for the template's routed engine family.
        normalized.SchemaVersion = TemplateSchemaVersionCatalog.GetCanonicalSchemaVersion(executionEngine);

        if (normalized.TemplateRevision <= 0)
        {
            normalized.TemplateRevision = 1;
        }

        if (string.IsNullOrWhiteSpace(normalized.CreatedWithAppVersion) || !Version.TryParse(normalized.CreatedWithAppVersion, out _))
        {
            normalized.CreatedWithAppVersion = "0.0.0";
        }

        if (string.IsNullOrWhiteSpace(normalized.TemplateType))
        {
            normalized.TemplateType = LabTemplate.SupportedTemplateType;
        }

        normalized.ExecutionEngine = executionEngine;
        normalized.LabNetworks = normalized.LabNetworks?.Select(Clone).ToList();
        normalized.ExtendedTopology = Clone(normalized.ExtendedTopology);

        for (var i = 0; i < normalized.VmTemplates.Count; i++)
        {
            var vm = normalized.VmTemplates[i];
            vm.GuestNetworkConfig = NormalizeGuestNetworkPlaceholder(vm.GuestNetworkConfig);
            NormalizeSwitchAssignments(vm);
            if (!string.IsNullOrWhiteSpace(vm.VmId))
            {
                continue;
            }

            normalized.VmTemplates[i] = new VmTemplate
            {
                VmId = Guid.NewGuid().ToString("N"),
                Name = vm.Name,
                MemoryMb = vm.MemoryMb,
                CpuCount = vm.CpuCount,
                VhdxId = vm.VhdxId,
                VhdPath = vm.VhdPath,
                VhdxSignature = vm.VhdxSignature,
                SwitchName = vm.SwitchName,
                SwitchNames = vm.SwitchNames?.ToList(),
                TimeZoneConfig = Clone(vm.TimeZoneConfig),
                SoftwareConfig = Clone(vm.SoftwareConfig),
                RoleConfig = Clone(vm.RoleConfig),
                GuestNetworkConfig = Clone(vm.GuestNetworkConfig),
                TopologyRole = vm.TopologyRole,
                CapabilityRoles = vm.CapabilityRoles?.ToList(),
                DependsOn = vm.DependsOn?.ToList(),
                CredentialSlots = Clone(vm.CredentialSlots),
                BootstrapProfileRef = vm.BootstrapProfileRef,
                Nics = vm.Nics?.Select(Clone).ToList()
            };
        }

        return normalized;
    }

    private static LabTemplate UpcastToCurrent(LabTemplate source)
    {
        // Deterministic N-1 -> N upcaster (single explicit hop for current support window).
        source.SchemaVersion = LabTemplate.CurrentSchemaVersion;
        return source;
    }

    private static bool IsLegacyV0Json(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("version", out var versionProperty))
            {
                return false;
            }

            return versionProperty.ValueKind == JsonValueKind.String
                   && string.Equals(versionProperty.GetString(), "v0", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static GuestNetworkStepConfig? NormalizeGuestNetworkPlaceholder(GuestNetworkStepConfig? config)
    {
        if (config == null)
        {
            return null;
        }

        var hasPayload =
            !string.IsNullOrWhiteSpace(config.IpAddress) ||
            !string.IsNullOrWhiteSpace(config.DefaultGateway) ||
            (config.DnsServers != null && config.DnsServers.Any(server => !string.IsNullOrWhiteSpace(server)));

        if (!config.Enabled && !hasPayload)
        {
            return null;
        }

        return config;
    }

    private static void NormalizeSwitchAssignments(VmTemplate vm)
    {
        var canonical = vm.SwitchNames?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList() ?? [];

        if (canonical.Count == 0 && !string.IsNullOrWhiteSpace(vm.SwitchName))
        {
            canonical.Add(vm.SwitchName.Trim());
        }

        vm.SwitchNames = canonical.Count > 0 ? canonical : null;
        vm.SwitchName = canonical.Count > 0 ? canonical[0] : null;
    }

    private static TimeZoneStepConfig? Clone(TimeZoneStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new TimeZoneStepConfig
        {
            Enabled = source.Enabled,
            TimeZoneId = source.TimeZoneId
        };
    }

    private static SoftwareStepConfig? Clone(SoftwareStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new SoftwareStepConfig
        {
            Enabled = source.Enabled,
            Packages = source.Packages?.ToList()
        };
    }

    private static RoleStepConfig? Clone(RoleStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new RoleStepConfig
        {
            Enabled = source.Enabled,
            Roles = source.Roles?.ToList()
        };
    }

    private static GuestNetworkStepConfig? Clone(GuestNetworkStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new GuestNetworkStepConfig
        {
            Enabled = source.Enabled,
            IpAddress = source.IpAddress,
            DefaultGateway = source.DefaultGateway,
            DnsServers = source.DnsServers?.ToList()
        };
    }

    private static LabNetworkTemplate Clone(LabNetworkTemplate source)
    {
        return new LabNetworkTemplate
        {
            NetworkId = source.NetworkId,
            Name = source.Name,
            SwitchName = source.SwitchName,
            Subnet = source.Subnet,
            Notes = source.Notes
        };
    }

    private static VmCredentialSlotBindings? Clone(VmCredentialSlotBindings? source)
    {
        if (source == null)
        {
            return null;
        }

        return new VmCredentialSlotBindings
        {
            LocalBootstrap = source.LocalBootstrap,
            DomainAdmin = source.DomainAdmin,
            DomainJoin = source.DomainJoin
        };
    }

    private static VmNetworkInterfaceTemplate Clone(VmNetworkInterfaceTemplate source)
    {
        return new VmNetworkInterfaceTemplate
        {
            NicId = source.NicId,
            Name = source.Name,
            NetworkId = source.NetworkId,
            SwitchName = source.SwitchName,
            IpAddress = source.IpAddress,
            PrefixLength = source.PrefixLength,
            DefaultGateway = source.DefaultGateway,
            DnsServers = source.DnsServers?.ToList()
        };
    }

    private static V2ExtendedTopologyTemplate? Clone(V2ExtendedTopologyTemplate? source)
    {
        if (source == null)
        {
            return null;
        }

        return new V2ExtendedTopologyTemplate
        {
            ChildDomains = source.ChildDomains?.Select(Clone).ToList(),
            AdditionalForests = source.AdditionalForests?.Select(Clone).ToList(),
            TreeDomains = source.TreeDomains?.Select(Clone).ToList()
        };
    }

    private static V2ChildDomainTemplate Clone(V2ChildDomainTemplate source)
    {
        return new V2ChildDomainTemplate
        {
            TopologyId = source.TopologyId,
            ParentDomainRef = source.ParentDomainRef,
            ChildLabel = source.ChildLabel,
            DomainFqdn = source.DomainFqdn,
            NetBiosName = source.NetBiosName,
            FirstDomainControllerVmId = source.FirstDomainControllerVmId
        };
    }

    private static V2AdditionalForestTemplate Clone(V2AdditionalForestTemplate source)
    {
        return new V2AdditionalForestTemplate
        {
            TopologyId = source.TopologyId,
            ForestRootDomainFqdn = source.ForestRootDomainFqdn,
            NetBiosName = source.NetBiosName,
            FirstDomainControllerVmId = source.FirstDomainControllerVmId
        };
    }

    private static V2TreeDomainTemplate Clone(V2TreeDomainTemplate source)
    {
        return new V2TreeDomainTemplate
        {
            TopologyId = source.TopologyId
        };
    }
}
