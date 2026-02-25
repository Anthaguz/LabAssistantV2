namespace LabAssistant.Services.Logging;

internal static class FileLogRotation
{
    public static void RotateIfNeeded(string activeFilePath, long incomingBytes, long maxActiveFileBytes, int retainedHistoryFiles)
    {
        if (string.IsNullOrWhiteSpace(activeFilePath) || maxActiveFileBytes <= 0)
        {
            return;
        }

        if (!File.Exists(activeFilePath))
        {
            return;
        }

        var currentLength = new FileInfo(activeFilePath).Length;
        if (currentLength + incomingBytes <= maxActiveFileBytes)
        {
            return;
        }

        retainedHistoryFiles = Math.Max(0, retainedHistoryFiles);
        if (retainedHistoryFiles == 0)
        {
            File.Delete(activeFilePath);
            return;
        }

        var oldest = GetRotatedFilePath(activeFilePath, retainedHistoryFiles);
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = retainedHistoryFiles - 1; index >= 1; index--)
        {
            var source = GetRotatedFilePath(activeFilePath, index);
            if (!File.Exists(source))
            {
                continue;
            }

            var target = GetRotatedFilePath(activeFilePath, index + 1);
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(source, target);
        }

        var firstRotated = GetRotatedFilePath(activeFilePath, 1);
        if (File.Exists(firstRotated))
        {
            File.Delete(firstRotated);
        }

        File.Move(activeFilePath, firstRotated);
    }

    public static string GetRotatedFilePath(string activeFilePath, int index)
    {
        if (index <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var directory = Path.GetDirectoryName(activeFilePath) ?? string.Empty;
        var extension = Path.GetExtension(activeFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(activeFilePath);
        var rotatedName = string.IsNullOrEmpty(extension)
            ? $"{fileNameWithoutExtension}.{index}"
            : $"{fileNameWithoutExtension}.{index}{extension}";

        return Path.Combine(directory, rotatedName);
    }
}
