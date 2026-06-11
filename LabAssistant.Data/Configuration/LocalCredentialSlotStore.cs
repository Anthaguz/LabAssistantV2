using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;

namespace LabAssistant.Data.Configuration;

public sealed class LocalCredentialSlotStore : ILocalCredentialSlotStore
{
    private readonly string _slotsFilePath;

    public LocalCredentialSlotStore(IAppPaths? paths = null)
    {
        var effectivePaths = paths ?? new AppPaths();
        _slotsFilePath = Path.Combine(effectivePaths.ConfigFolder, "credential-slots.json");
    }

    public IReadOnlyList<LocalCredentialSlotDefinition> LoadDefinitions()
    {
        return LoadRecords()
            .Select(record => new LocalCredentialSlotDefinition
            {
                SlotKey = record.SlotKey,
                Username = record.Username
            })
            .OrderBy(record => record.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryGetCredential(string slotKey, out V2RuntimeCredential credential)
    {
        credential = new V2RuntimeCredential();
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            return false;
        }

        var record = LoadRecords().FirstOrDefault(candidate =>
            string.Equals(candidate.SlotKey, slotKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (record is null)
        {
            return false;
        }

        credential = new V2RuntimeCredential
        {
            Username = record.Username,
            Password = Unprotect(record.ProtectedPassword)
        };
        return true;
    }

    public void Upsert(string slotKey, string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var normalizedSlotKey = slotKey.Trim();
        var normalizedUsername = username.Trim();
        var records = LoadRecords().ToList();
        var existingIndex = records.FindIndex(candidate =>
            string.Equals(candidate.SlotKey, normalizedSlotKey, StringComparison.OrdinalIgnoreCase));
        var updated = new LocalCredentialSlotRecord
        {
            SlotKey = normalizedSlotKey,
            Username = normalizedUsername,
            ProtectedPassword = Protect(password)
        };

        if (existingIndex >= 0)
        {
            records[existingIndex] = updated;
        }
        else
        {
            records.Add(updated);
        }

        SaveRecords(records);
    }

    private IReadOnlyList<LocalCredentialSlotRecord> LoadRecords()
    {
        var directory = Path.GetDirectoryName(_slotsFilePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_slotsFilePath))
        {
            return Array.Empty<LocalCredentialSlotRecord>();
        }

        try
        {
            var json = File.ReadAllText(_slotsFilePath);
            var records = JsonSerializer.Deserialize<List<LocalCredentialSlotRecord>>(json);
            return records ?? [];
        }
        catch
        {
            return Array.Empty<LocalCredentialSlotRecord>();
        }
    }

    private void SaveRecords(IReadOnlyList<LocalCredentialSlotRecord> records)
    {
        var directory = Path.GetDirectoryName(_slotsFilePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ordered = records
            .OrderBy(record => record.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_slotsFilePath, json);
    }

    private static string Protect(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var protectedBytes = ProtectForCurrentUser(bytes);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string protectedPassword)
    {
        var protectedBytes = Convert.FromBase64String(protectedPassword);
        var bytes = UnprotectForCurrentUser(protectedBytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] ProtectForCurrentUser(byte[] data)
    {
        var input = new DATA_BLOB();
        var output = new DATA_BLOB();
        try
        {
            input.pbData = Marshal.AllocHGlobal(data.Length);
            input.cbData = data.Length;
            Marshal.Copy(data, 0, input.pbData, data.Length);

            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
            {
                throw new InvalidOperationException($"DPAPI protect failed with error {Marshal.GetLastWin32Error()}.");
            }

            var protectedBytes = new byte[output.cbData];
            Marshal.Copy(output.pbData, protectedBytes, 0, output.cbData);
            return protectedBytes;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }

            if (output.pbData != IntPtr.Zero)
            {
                LocalFree(output.pbData);
            }
        }
    }

    private static byte[] UnprotectForCurrentUser(byte[] data)
    {
        var input = new DATA_BLOB();
        var output = new DATA_BLOB();
        try
        {
            input.pbData = Marshal.AllocHGlobal(data.Length);
            input.cbData = data.Length;
            Marshal.Copy(data, 0, input.pbData, data.Length);

            if (!CryptUnprotectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
            {
                throw new InvalidOperationException($"DPAPI unprotect failed with error {Marshal.GetLastWin32Error()}.");
            }

            var unprotectedBytes = new byte[output.cbData];
            Marshal.Copy(output.pbData, unprotectedBytes, 0, output.cbData);
            return unprotectedBytes;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.pbData);
            }

            if (output.pbData != IntPtr.Zero)
            {
                LocalFree(output.pbData);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        string? ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private sealed class LocalCredentialSlotRecord
    {
        public string SlotKey { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string ProtectedPassword { get; set; } = string.Empty;
    }
}
