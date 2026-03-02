using System.Runtime.InteropServices;
using System.Text;

namespace LabAssistant.WinUI.Interop;

internal static class NativeFileDialogs
{
    private const int MaxPath = 4096;
    private const int OfnOverwritePrompt = 0x00000002;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;
    private const string JsonFilter = "JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0\0";

    public static string? ShowOpenJsonDialog(nint ownerWindow)
    {
        var fileBuffer = new StringBuilder(MaxPath);
        var dialog = CreateDialog(ownerWindow, fileBuffer);
        dialog.lpstrTitle = "Import Template";
        dialog.Flags = OfnExplorer | OfnPathMustExist | OfnFileMustExist;
        return GetOpenFileName(dialog) ? fileBuffer.ToString() : null;
    }

    public static string? ShowSaveJsonDialog(nint ownerWindow, string suggestedFileName)
    {
        var fileBuffer = new StringBuilder(MaxPath);
        if (!string.IsNullOrWhiteSpace(suggestedFileName))
        {
            fileBuffer.Append(suggestedFileName);
        }

        var dialog = CreateDialog(ownerWindow, fileBuffer);
        dialog.lpstrTitle = "Export Template";
        dialog.Flags = OfnExplorer | OfnPathMustExist | OfnOverwritePrompt;
        return GetSaveFileName(dialog) ? EnsureJsonExtension(fileBuffer.ToString()) : null;
    }

    private static OPENFILENAME CreateDialog(nint ownerWindow, StringBuilder fileBuffer)
    {
        return new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            hwndOwner = ownerWindow,
            lpstrFilter = JsonFilter,
            nFilterIndex = 1,
            lpstrFile = fileBuffer,
            nMaxFile = MaxPath,
            lpstrDefExt = "json"
        };
    }

    private static string EnsureJsonExtension(string path)
    {
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path + ".json";
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName([In, Out] OPENFILENAME dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName([In, Out] OPENFILENAME dialog);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class OPENFILENAME
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public string lpstrFilter = string.Empty;
        public string lpstrCustomFilter = string.Empty;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public StringBuilder lpstrFile = new();
        public int nMaxFile;
        public StringBuilder lpstrFileTitle = new();
        public int nMaxFileTitle;
        public string lpstrInitialDir = string.Empty;
        public string lpstrTitle = string.Empty;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt = string.Empty;
        public nint lCustData;
        public nint lpfnHook;
        public string lpTemplateName = string.Empty;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
