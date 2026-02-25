using System.IO;

namespace LabAssistant.Services.FileSystem;

public sealed class DestinationPathFeasibilityProbe : IDestinationPathFeasibilityProbe
{
    public DestinationPathFeasibilityResult Probe(
        string? path,
        DestinationPathTargetKind targetKind,
        bool verifyWriteAccess)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.Missing,
                Path = string.Empty,
                Message = "Destination path is missing."
            };
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.Invalid,
                Path = path.Trim(),
                Message = $"Destination path is invalid: {path.Trim()}",
                Detail = ex.Message
            };
        }

        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath) || !IsRootReachable(rootPath))
        {
            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.RootUnavailable,
                Path = fullPath,
                RootPath = rootPath,
                Message = $"Destination path root is unavailable: {fullPath}"
            };
        }

        if (targetKind == DestinationPathTargetKind.Directory)
        {
            if (File.Exists(fullPath))
            {
                return new DestinationPathFeasibilityResult
                {
                    Status = DestinationPathFeasibilityStatus.ExistingFileConflict,
                    Path = fullPath,
                    RootPath = rootPath,
                    Message = $"A file already exists at the VM destination path: {fullPath}"
                };
            }

            if (Directory.Exists(fullPath))
            {
                return new DestinationPathFeasibilityResult
                {
                    Status = DestinationPathFeasibilityStatus.ExistingDirectoryConflict,
                    Path = fullPath,
                    RootPath = rootPath,
                    Message = $"The VM destination folder already exists: {fullPath}"
                };
            }
        }
        else
        {
            if (Directory.Exists(fullPath))
            {
                return new DestinationPathFeasibilityResult
                {
                    Status = DestinationPathFeasibilityStatus.ExistingDirectoryConflict,
                    Path = fullPath,
                    RootPath = rootPath,
                    Message = $"A directory already exists at the differencing disk path: {fullPath}"
                };
            }

            if (File.Exists(fullPath))
            {
                return new DestinationPathFeasibilityResult
                {
                    Status = DestinationPathFeasibilityStatus.ExistingFileConflict,
                    Path = fullPath,
                    RootPath = rootPath,
                    Message = $"The differencing disk path already exists: {fullPath}"
                };
            }
        }

        if (!verifyWriteAccess)
        {
            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.Valid,
                Path = fullPath,
                RootPath = rootPath,
                Message = "Destination path passed quick checks."
            };
        }

        var probeDirectory = ResolveProbeDirectory(fullPath, targetKind);
        if (string.IsNullOrWhiteSpace(probeDirectory))
        {
            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.RootUnavailable,
                Path = fullPath,
                RootPath = rootPath,
                Message = $"No reachable parent directory was found for destination path: {fullPath}"
            };
        }

        try
        {
            var probeFile = Path.Combine(probeDirectory, $".la-write-probe-{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(
                probeFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                options: FileOptions.DeleteOnClose))
            {
                stream.WriteByte(0);
                stream.Flush();
            }

            if (File.Exists(probeFile))
            {
                File.Delete(probeFile);
            }

            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.Valid,
                Path = fullPath,
                RootPath = rootPath,
                Message = "Destination path passed full checks."
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
        {
            return new DestinationPathFeasibilityResult
            {
                Status = DestinationPathFeasibilityStatus.Unwritable,
                Path = fullPath,
                RootPath = rootPath,
                Message = $"Destination path is not writable: {fullPath}",
                Detail = ex.Message
            };
        }
    }

    private static bool IsRootReachable(string rootPath)
    {
        try
        {
            return Directory.Exists(rootPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? ResolveProbeDirectory(string fullPath, DestinationPathTargetKind targetKind)
    {
        var current = targetKind == DestinationPathTargetKind.File
            ? Path.GetDirectoryName(fullPath)
            : fullPath;

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }
}
