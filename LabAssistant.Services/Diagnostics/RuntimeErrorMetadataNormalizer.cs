using System.Text.RegularExpressions;

namespace LabAssistant.Services.Diagnostics;

public static partial class RuntimeErrorMetadataNormalizer
{
    public static IReadOnlyDictionary<string, object?> FromException(Exception exception)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["exceptionType"] = exception.GetType().Name
        };

        if (exception.HResult != 0)
        {
            metadata["hresult"] = FormatHResult(exception.HResult);
        }

        return metadata;
    }

    public static IReadOnlyDictionary<string, object?> FromPowerShellErrorText(string? errorText)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(errorText))
        {
            return metadata;
        }

        var exceptionType = TryExtractExceptionType(errorText);
        if (!string.IsNullOrWhiteSpace(exceptionType))
        {
            metadata["exceptionType"] = exceptionType;
        }

        var hresult = TryExtractHResult(errorText);
        if (!string.IsNullOrWhiteSpace(hresult))
        {
            metadata["hresult"] = hresult;
        }

        var errorCode = TryExtractErrorCode(errorText);
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            metadata["errorCode"] = errorCode;
        }

        return metadata;
    }

    public static IReadOnlyDictionary<string, object?> Merge(
        IReadOnlyDictionary<string, object?>? baseMetadata,
        IReadOnlyDictionary<string, object?>? extraMetadata)
    {
        if ((baseMetadata == null || baseMetadata.Count == 0) && (extraMetadata == null || extraMetadata.Count == 0))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (baseMetadata != null)
        {
            foreach (var pair in baseMetadata)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        if (extraMetadata != null)
        {
            foreach (var pair in extraMetadata)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private static string? TryExtractExceptionType(string errorText)
    {
        var fullyQualifiedMatch = ExceptionTypeRegex().Match(errorText);
        if (!fullyQualifiedMatch.Success)
        {
            return null;
        }

        var raw = fullyQualifiedMatch.Groups["type"].Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var lastDot = raw.LastIndexOf('.');
        return lastDot >= 0 ? raw[(lastDot + 1)..] : raw;
    }

    private static string? TryExtractHResult(string errorText)
    {
        var match = HResultRegex().Match(errorText);
        if (!match.Success)
        {
            return null;
        }

        var digits = match.Value[2..].ToUpperInvariant();
        return $"0x{digits}";
    }

    private static string? TryExtractErrorCode(string errorText)
    {
        var fqid = FullyQualifiedErrorIdRegex().Match(errorText);
        if (fqid.Success)
        {
            var value = fqid.Groups["id"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var operationFailed = OperationFailedRegex().Match(errorText);
        if (operationFailed.Success)
        {
            return operationFailed.Groups["code"].Value;
        }

        return null;
    }

    private static string FormatHResult(int hresult)
        => $"0x{hresult:X8}";

    [GeneratedRegex(@"(?<type>(?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*(?:Exception|Error))\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex ExceptionTypeRegex();

    [GeneratedRegex(@"\b0x[0-9A-Fa-f]{8}\b", RegexOptions.CultureInvariant)]
    private static partial Regex HResultRegex();

    [GeneratedRegex(@"FullyQualifiedErrorId\s*:\s*(?<id>[^,\r\n]+)", RegexOptions.CultureInvariant)]
    private static partial Regex FullyQualifiedErrorIdRegex();

    [GeneratedRegex(@"\b(?<code>OperationFailed)\b", RegexOptions.CultureInvariant)]
    private static partial Regex OperationFailedRegex();
}
