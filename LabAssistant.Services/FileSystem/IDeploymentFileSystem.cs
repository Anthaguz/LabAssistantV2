namespace LabAssistant.Services.FileSystem;

public interface IDeploymentFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    bool DeleteDirectory(string path, out string? error);
    bool DeleteFile(string path, out string? error);
}
