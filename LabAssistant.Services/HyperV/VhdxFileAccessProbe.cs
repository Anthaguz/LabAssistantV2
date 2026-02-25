namespace LabAssistant.Services.HyperV;

public sealed class VhdxFileAccessProbe : IVhdxFileAccessProbe
{
    public VhdxFileAccessProbeResult Probe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new VhdxFileAccessProbeResult
            {
                Status = VhdxFileAccessStatus.Missing,
                Path = path ?? string.Empty,
                Detail = "Path is empty."
            };
        }

        try
        {
            if (!File.Exists(path))
            {
                return new VhdxFileAccessProbeResult
                {
                    Status = VhdxFileAccessStatus.Missing,
                    Path = path
                };
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _ = stream.Length;

            return new VhdxFileAccessProbeResult
            {
                Status = VhdxFileAccessStatus.Accessible,
                Path = path
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new VhdxFileAccessProbeResult
            {
                Status = VhdxFileAccessStatus.Unreadable,
                Path = path,
                Detail = ex.Message
            };
        }
        catch (IOException ex)
        {
            return new VhdxFileAccessProbeResult
            {
                Status = VhdxFileAccessStatus.Unreadable,
                Path = path,
                Detail = ex.Message
            };
        }
    }
}
