using System.IO;

namespace LabAssistant.Services.FileSystem;

public sealed class FreeSpaceInfoProvider : IFreeSpaceInfoProvider
{
    public FreeSpaceProbeResult Probe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Unknown,
                Path = string.Empty,
                Message = "Destination path is missing."
            };
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception)
        {
            return new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Unknown,
                Path = path.Trim(),
                Message = "Free space could not be determined for an invalid path."
            };
        }

        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Unknown,
                Path = fullPath,
                Message = "Free space could not be determined because the path root is unavailable."
            };
        }

        try
        {
            var drive = new DriveInfo(rootPath);
            if (!drive.IsReady)
            {
                return new FreeSpaceProbeResult
                {
                    Status = FreeSpaceProbeStatus.Unknown,
                    Path = fullPath,
                    RootPath = rootPath,
                    Message = $"Free space could not be determined because drive '{rootPath}' is not ready."
                };
            }

            return new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Known,
                Path = fullPath,
                RootPath = rootPath,
                AvailableBytes = drive.AvailableFreeSpace,
                Message = "Free space measured successfully."
            };
        }
        catch (Exception)
        {
            return new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Unknown,
                Path = fullPath,
                RootPath = rootPath,
                Message = $"Free space could not be determined for root '{rootPath}'."
            };
        }
    }
}
