using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LabAssistant.Diagnostics.CodeGen;

/// <summary>
/// Source generator that turns <c>Diagnostics/status-codes.yaml</c> into the strongly-typed
/// <c>LaStatus</c> constants and the <c>StatusCodeCatalog</c> data, in namespace
/// <c>LabAssistant.Services.Diagnostics</c>. The numeric code is the single source of truth; the
/// generator composes it from severity, flags, facility, operation, and status, and fails the build
/// on any out-of-range field, dangling reference, or duplicate composed code.
/// </summary>
[Generator]
public sealed class StatusCodeGenerator : IIncrementalGenerator
{
    private const string TargetFileSuffix = "status-codes.yaml";
    private const string Namespace = "LabAssistant.Services.Diagnostics";

    private static readonly DiagnosticDescriptor RegistryError = new(
        id: "LASTATUS001",
        title: "Invalid status-code registry",
        messageFormat: "status-codes.yaml is invalid: {0}",
        category: "LabAssistant.Diagnostics",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(static text => text.Path.Replace('\\', '/')
                .EndsWith(TargetFileSuffix, StringComparison.OrdinalIgnoreCase));

        var contents = files.Select(static (text, ct) => text.GetText(ct)?.ToString() ?? string.Empty);

        context.RegisterSourceOutput(contents.Collect(), static (spc, yamlDocs) =>
        {
            var yaml = yamlDocs.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
            if (string.IsNullOrWhiteSpace(yaml))
            {
                // No registry present in this compilation; emit nothing.
                return;
            }

            try
            {
                var registry = Parse(yaml!);
                var errors = new List<string>();
                var codes = Compose(registry, errors);

                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(RegistryError, Location.None, error));
                    }

                    return;
                }

                spc.AddSource("LaStatus.g.cs", SourceText.From(EmitLaStatus(codes), Encoding.UTF8));
                spc.AddSource("StatusCodeCatalog.Generated.g.cs", SourceText.From(EmitCatalog(registry, codes), Encoding.UTF8));
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(RegistryError, Location.None, ex.Message));
            }
        });
    }

    private static RegistryDto Parse(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<RegistryDto>(yaml) ?? new RegistryDto();
    }

    private static List<ComposedCode> Compose(RegistryDto registry, List<string> errors)
    {
        var result = new List<ComposedCode>();

        var severities = new Dictionary<string, (byte Value, string Level)>(StringComparer.OrdinalIgnoreCase);
        var severityValues = new HashSet<byte>();
        foreach (var severity in registry.Severities ?? new List<SeverityDto>())
        {
            if (severity.Name is null) { errors.Add("a severity is missing 'name'."); continue; }
            if (!TryParseByte(severity.Value, out var sv) || sv > 0xF)
            {
                errors.Add($"severity '{severity.Name}' has an invalid nibble value '{severity.Value}'.");
                continue;
            }

            if (severities.ContainsKey(severity.Name))
            {
                errors.Add($"severity '{severity.Name}' is defined more than once.");
                continue;
            }

            if (!severityValues.Add(sv))
            {
                errors.Add($"severity value 0x{sv:X} ('{severity.Name}') is defined more than once.");
                continue;
            }

            severities[severity.Name] = (sv, severity.Level ?? "info");
        }

        var flags = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var flagBitsSeen = new HashSet<byte>();
        foreach (var flag in registry.Flags ?? new List<FlagDto>())
        {
            if (flag.Name is null) { errors.Add("a flag is missing 'name'."); continue; }
            if (!TryParseByte(flag.Bit, out var fb) || fb > 0xF)
            {
                errors.Add($"flag '{flag.Name}' has an invalid nibble bit '{flag.Bit}'.");
                continue;
            }

            // Flags are OR-combined into the composed code's flags nibble, so each must occupy exactly one bit.
            // A non-single-bit value (0, or a multi-bit mask like 0x3) would decode as several unrelated flags,
            // silently making HasFlag return true for a flag that was never assigned.
            if (fb == 0 || (fb & (fb - 1)) != 0)
            {
                errors.Add($"flag '{flag.Name}' has a non-single-bit value '{flag.Bit}'; each flag must be exactly one of 0x1, 0x2, 0x4, 0x8.");
                continue;
            }

            if (flags.ContainsKey(flag.Name))
            {
                errors.Add($"flag '{flag.Name}' is defined more than once.");
                continue;
            }

            if (!flagBitsSeen.Add(fb))
            {
                errors.Add($"flag bit 0x{fb:X} ('{flag.Name}') is defined more than once.");
                continue;
            }

            flags[flag.Name] = fb;
        }

        var phases = new HashSet<string>(registry.Phases ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        // facility value -> (name, title, operation value -> operation name)
        var facilities = new Dictionary<byte, FacilityInfo>();
        var facilityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var facility in registry.Facilities ?? new List<FacilityDto>())
        {
            if (facility.Name is null) { errors.Add("a facility is missing 'name'."); continue; }
            if (!TryParseByte(facility.Value, out var fv))
            {
                errors.Add($"facility '{facility.Name}' has an invalid value '{facility.Value}'.");
                continue;
            }

            if (facilities.ContainsKey(fv))
            {
                errors.Add($"facility value 0x{fv:X2} ('{facility.Name}') is defined more than once.");
                continue;
            }

            if (!facilityNames.Add(facility.Name))
            {
                errors.Add($"facility name '{facility.Name}' is defined more than once.");
                continue;
            }

            var ops = new Dictionary<byte, string>();
            var opNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var op in facility.Operations ?? new List<OperationDto>())
            {
                if (op.Name is null) { errors.Add($"an operation under facility '{facility.Name}' is missing 'name'."); continue; }
                if (!TryParseByte(op.Value, out var ov))
                {
                    errors.Add($"operation '{op.Name}' under '{facility.Name}' has an invalid value '{op.Value}'.");
                    continue;
                }

                if (ops.ContainsKey(ov))
                {
                    errors.Add($"operation value 0x{ov:X2} under facility '{facility.Name}' is defined more than once.");
                    continue;
                }

                if (!opNames.Add(op.Name))
                {
                    errors.Add($"operation name '{op.Name}' under facility '{facility.Name}' is defined more than once.");
                    continue;
                }

                ops[ov] = op.Name;
            }

            facilities[fv] = new FacilityInfo(fv, facility.Name, facility.Title ?? facility.Name, ops);
        }

        var usedCodes = new Dictionary<uint, string>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var code in registry.Codes ?? new List<CodeDto>())
        {
            if (!TryParseByte(code.Facility, out var fv) || !facilities.TryGetValue(fv, out var facility))
            {
                errors.Add($"code references unknown facility '{code.Facility}'.");
                continue;
            }

            if (!TryParseByte(code.Operation, out var ov) || !facility.Operations.TryGetValue(ov, out var operationName))
            {
                errors.Add($"code under '{facility.Name}' references unknown operation '{code.Operation}'.");
                continue;
            }

            if (!TryParseByte(code.Status, out var sv))
            {
                errors.Add($"code {facility.Name}.{operationName} has an invalid status '{code.Status}'.");
                continue;
            }

            if (code.Severity is null || !severities.TryGetValue(code.Severity, out var severity))
            {
                errors.Add($"code {facility.Name}.{operationName} references unknown severity '{code.Severity}'.");
                continue;
            }

            byte flagBits = 0;
            foreach (var flagName in code.Flags ?? new List<string>())
            {
                if (!flags.TryGetValue(flagName, out var bit))
                {
                    errors.Add($"code {facility.Name}.{operationName} references unknown flag '{flagName}'.");
                    continue;
                }

                flagBits |= bit;
            }

            var phase = code.Phase ?? "atomic";
            if (!phases.Contains(phase))
            {
                errors.Add($"code {facility.Name}.{operationName} references unknown phase '{phase}'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(code.Message))
            {
                errors.Add($"code {facility.Name}.{operationName} is missing 'message'.");
                continue;
            }

            uint composed = ((uint)severity.Value << 28)
                | ((uint)flagBits << 24)
                | ((uint)fv << 16)
                | ((uint)ov << 8)
                | sv;

            if (usedCodes.TryGetValue(composed, out var existing))
            {
                errors.Add($"code {facility.Name}.{operationName} ('{code.Title}') composes to 0x{composed:X8} which collides with '{existing}'.");
                continue;
            }

            usedCodes[composed] = $"{facility.Name}.{operationName} ('{code.Title}')";

            var constName = MakeUniqueConstName(facility.Name, code.Title, operationName, sv, usedNames);
            var dotted = phase.Equals("atomic", StringComparison.OrdinalIgnoreCase)
                ? $"{facility.Name}.{operationName}"
                : $"{facility.Name}.{operationName}.{phase}";

            result.Add(new ComposedCode(
                composed,
                constName,
                facility.Name,
                operationName,
                dotted,
                code.Title,
                code.Message!,
                code.Remediation,
                severity.Level,
                PhaseToEnum(phase)));
        }

        return result;
    }

    private static string EmitLaStatus(List<ComposedCode> codes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> Generated from Diagnostics/status-codes.yaml. Do not edit.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {Namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Canonical 32-bit logging status codes, generated from the registry.</summary>");
        sb.AppendLine("public static class LaStatus");
        sb.AppendLine("{");
        foreach (var code in codes.OrderBy(c => c.Code))
        {
            sb.AppendLine($"    /// <summary>{Xml(code.Title ?? code.Message)} (0x{code.Code:X8}, {code.DottedName}).</summary>");
            sb.AppendLine($"    public const uint {code.ConstName} = 0x{code.Code:X8}u;");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EmitCatalog(RegistryDto registry, List<ComposedCode> codes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> Generated from Diagnostics/status-codes.yaml. Do not edit.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"namespace {Namespace};");
        sb.AppendLine();
        sb.AppendLine("public static partial class StatusCodeCatalog");
        sb.AppendLine("{");

        sb.AppendLine("    internal static StatusCodeDescriptor[] BuildAll()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new StatusCodeDescriptor[]");
        sb.AppendLine("        {");
        foreach (var code in codes.OrderBy(c => c.Code))
        {
            sb.Append("            new StatusCodeDescriptor(");
            sb.Append($"0x{code.Code:X8}u, ");
            sb.Append($"{Lit(code.FacilityName)}, ");
            sb.Append($"{Lit(code.OperationName)}, ");
            sb.Append($"{Lit(code.DottedName)}, ");
            sb.Append($"{Lit(code.Title)}, ");
            sb.Append($"{Lit(code.Message)}, ");
            sb.Append($"{Lit(code.Remediation)}, ");
            sb.Append($"{Lit(code.Level)}, ");
            sb.Append($"StatusCodePhase.{code.Phase}");
            sb.AppendLine("),");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    internal static StatusFacility[] BuildFacilities()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new StatusFacility[]");
        sb.AppendLine("        {");
        foreach (var facility in (registry.Facilities ?? new List<FacilityDto>()).Where(f => f.Name != null))
        {
            if (!TryParseByte(facility.Value, out var fv))
            {
                continue;
            }

            sb.AppendLine($"            new StatusFacility(0x{fv:X2}, {Lit(facility.Name)}, {Lit(facility.Title ?? facility.Name)}),");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string MakeUniqueConstName(string facilityName, string? title, string operationName, byte status, HashSet<string> used)
    {
        var facilityToken = Pascal(facilityName);
        var suffix = !string.IsNullOrWhiteSpace(title) ? Pascal(title!) : Pascal(operationName);
        var baseName = $"{facilityToken}_{suffix}";
        var candidate = baseName;
        if (used.Contains(candidate))
        {
            candidate = $"{baseName}_{status:X2}";
        }

        var counter = 2;
        while (used.Contains(candidate))
        {
            candidate = $"{baseName}_{status:X2}_{counter++}";
        }

        used.Add(candidate);
        return candidate;
    }

    private static string Pascal(string value)
    {
        var sb = new StringBuilder();
        var upperNext = true;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (sb.Length == 0 && char.IsDigit(ch))
                {
                    sb.Append('_');
                }

                sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        return sb.Length == 0 ? "Code" : sb.ToString();
    }

    private static StatusCodePhaseToken PhaseToEnum(string phase)
    {
        return phase.ToLowerInvariant() switch
        {
            "start" => StatusCodePhaseToken.Start,
            "progress" => StatusCodePhaseToken.Progress,
            "end" => StatusCodePhaseToken.End,
            _ => StatusCodePhaseToken.Atomic
        };
    }

    private static bool TryParseByte(string? raw, out byte value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw!.Trim();
        try
        {
            uint parsed = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt32(raw.Substring(2), 16)
                : uint.Parse(raw, CultureInfo.InvariantCulture);

            if (parsed > 0xFF)
            {
                return false;
            }

            value = (byte)parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Lit(string? value)
    {
        return value is null
            ? "null"
            : SymbolDisplay.FormatLiteral(value, quote: true);
    }

    private static string Xml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private enum StatusCodePhaseToken { Start, Progress, End, Atomic }

    private sealed class FacilityInfo
    {
        public FacilityInfo(byte value, string name, string title, Dictionary<byte, string> operations)
        {
            Value = value;
            Name = name;
            Title = title;
            Operations = operations;
        }

        public byte Value { get; }
        public string Name { get; }
        public string Title { get; }
        public Dictionary<byte, string> Operations { get; }
    }

    private sealed class ComposedCode
    {
        public ComposedCode(uint code, string constName, string facilityName, string operationName, string dottedName, string? title, string message, string? remediation, string level, StatusCodePhaseToken phase)
        {
            Code = code;
            ConstName = constName;
            FacilityName = facilityName;
            OperationName = operationName;
            DottedName = dottedName;
            Title = title;
            Message = message;
            Remediation = remediation;
            Level = level;
            Phase = phase;
        }

        public uint Code { get; }
        public string ConstName { get; }
        public string FacilityName { get; }
        public string OperationName { get; }
        public string DottedName { get; }
        public string? Title { get; }
        public string Message { get; }
        public string? Remediation { get; }
        public string Level { get; }
        public StatusCodePhaseToken Phase { get; }
    }

    // --- YAML DTOs (numbers parsed as strings to accept 0x-prefixed hex robustly) ---

    private sealed class RegistryDto
    {
        public List<SeverityDto> Severities { get; set; } = new();
        public List<FlagDto> Flags { get; set; } = new();
        public List<string> Phases { get; set; } = new();
        public List<FacilityDto> Facilities { get; set; } = new();
        public List<CodeDto> Codes { get; set; } = new();
    }

    private sealed class SeverityDto
    {
        public string? Value { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
    }

    private sealed class FlagDto
    {
        public string? Bit { get; set; }
        public string? Name { get; set; }
    }

    private sealed class FacilityDto
    {
        public string? Value { get; set; }
        public string? Name { get; set; }
        public string? Title { get; set; }
        public List<OperationDto>? Operations { get; set; }
    }

    private sealed class OperationDto
    {
        public string? Value { get; set; }
        public string? Name { get; set; }
        public string? Title { get; set; }
    }

    private sealed class CodeDto
    {
        public string? Facility { get; set; }
        public string? Operation { get; set; }
        public string? Status { get; set; }
        public string? Severity { get; set; }
        public List<string>? Flags { get; set; }
        public string? Phase { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Remediation { get; set; }
    }
}
