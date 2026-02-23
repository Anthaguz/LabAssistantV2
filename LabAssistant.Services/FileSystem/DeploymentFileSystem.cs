using System;
using System.IO;

namespace LabAssistant.Services.FileSystem;

public sealed class DeploymentFileSystem : IDeploymentFileSystem
{
    public bool DirectoryExists(string path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    public bool FileExists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public bool DeleteDirectory(string path, out string? error)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool DeleteFile(string path, out string? error)
    {
        try
        {
            File.Delete(path);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
