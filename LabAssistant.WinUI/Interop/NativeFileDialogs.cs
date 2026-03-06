using System.Runtime.InteropServices;

namespace LabAssistant.WinUI.Interop;

internal static class NativeFileDialogs
{
    private const int MaxPathChars = 4096;
    private const int MaxFileTitleChars = 512;
    private const int OfnOverwritePrompt = 0x00000002;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;
    private const int OfnNoChangeDir = 0x00000008;
    private const string JsonFilter = "JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0\0";
    private const string VhdxFilter = "VHDX files (*.vhdx)\0*.vhdx\0All files (*.*)\0*.*\0\0";

    public static string? ShowOpenJsonDialog(nint ownerWindow)
    {
        using var context = CreateDialogContext(ownerWindow, JsonFilter, "json");
        context.Dialog.lpstrTitle = "Import Template";
        context.Dialog.Flags = OfnExplorer | OfnPathMustExist | OfnFileMustExist | OfnNoChangeDir;
        if (GetOpenFileName(ref context.Dialog))
        {
            return ReadNullTerminatedString(context.Dialog.lpstrFile);
        }

        ThrowIfDialogError("Open file dialog failed");
        return null;
    }

    public static string? ShowSaveJsonDialog(nint ownerWindow, string suggestedFileName)
    {
        using var context = CreateDialogContext(ownerWindow, JsonFilter, "json");
        context.Dialog.lpstrTitle = "Export Template";
        context.Dialog.Flags = OfnExplorer | OfnPathMustExist | OfnOverwritePrompt | OfnNoChangeDir;
        if (!string.IsNullOrWhiteSpace(suggestedFileName))
        {
            WriteNullTerminatedString(context.Dialog.lpstrFile, MaxPathChars, EnsureJsonExtension(suggestedFileName));
        }

        if (GetSaveFileName(ref context.Dialog))
        {
            return EnsureJsonExtension(ReadNullTerminatedString(context.Dialog.lpstrFile));
        }

        ThrowIfDialogError("Save file dialog failed");
        return null;
    }

    public static string? ShowOpenVhdxDialog(nint ownerWindow)
    {
        using var context = CreateDialogContext(ownerWindow, VhdxFilter, "vhdx");
        context.Dialog.lpstrTitle = "Select Base Disk";
        context.Dialog.Flags = OfnExplorer | OfnPathMustExist | OfnFileMustExist | OfnNoChangeDir;
        if (GetOpenFileName(ref context.Dialog))
        {
            return ReadNullTerminatedString(context.Dialog.lpstrFile);
        }

        ThrowIfDialogError("Open VHDX dialog failed");
        return null;
    }

    private static DialogContext CreateDialogContext(nint ownerWindow, string filter, string defaultExtension)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPathChars * sizeof(char));
        var fileTitleBuffer = Marshal.AllocHGlobal(MaxFileTitleChars * sizeof(char));
        ZeroMemory(fileBuffer, MaxPathChars * sizeof(char));
        ZeroMemory(fileTitleBuffer, MaxFileTitleChars * sizeof(char));

        var dialog = new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            hwndOwner = ownerWindow,
            lpstrFilter = filter,
            nFilterIndex = 1,
            lpstrFile = fileBuffer,
            nMaxFile = MaxPathChars,
            lpstrFileTitle = fileTitleBuffer,
            nMaxFileTitle = MaxFileTitleChars,
            lpstrInitialDir = string.Empty,
            lpstrDefExt = defaultExtension,
            lpstrCustomFilter = string.Empty,
            lpstrTitle = string.Empty,
            lpTemplateName = string.Empty
        };

        return new DialogContext(dialog, fileBuffer, fileTitleBuffer);
    }

    private static void ThrowIfDialogError(string prefix)
    {
        var extendedError = CommDlgExtendedError();
        if (extendedError != 0)
        {
            throw new InvalidOperationException($"{prefix} (0x{extendedError:X8}).");
        }
    }

    private static string EnsureJsonExtension(string path)
    {
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path + ".json";
    }

    private static string ReadNullTerminatedString(nint buffer)
    {
        return Marshal.PtrToStringUni(buffer) ?? string.Empty;
    }

    private static void WriteNullTerminatedString(nint buffer, int maxChars, string value)
    {
        var truncated = value.Length >= maxChars ? value[..(maxChars - 1)] : value;
        for (var i = 0; i < maxChars; i++)
        {
            Marshal.WriteInt16(buffer, i * sizeof(char), 0);
        }

        Marshal.Copy(truncated.ToCharArray(), 0, buffer, truncated.Length);
    }

    private static void ZeroMemory(nint ptr, int bytes)
    {
        for (var i = 0; i < bytes; i++)
        {
            Marshal.WriteByte(ptr, i, 0);
        }
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName([In, Out] ref OPENFILENAME dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName([In, Out] ref OPENFILENAME dialog);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpstrFilter;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpstrInitialDir;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    private sealed class DialogContext : IDisposable
    {
        public DialogContext(OPENFILENAME dialog, nint fileBuffer, nint fileTitleBuffer)
        {
            Dialog = dialog;
            _fileBuffer = fileBuffer;
            _fileTitleBuffer = fileTitleBuffer;
        }

        public OPENFILENAME Dialog;
        private readonly nint _fileBuffer;
        private readonly nint _fileTitleBuffer;

        public void Dispose()
        {
            if (_fileBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(_fileBuffer);
            }

            if (_fileTitleBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(_fileTitleBuffer);
            }
        }
    }
}
