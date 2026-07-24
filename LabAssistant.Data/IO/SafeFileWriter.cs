using System;
using System.IO;
using System.Text;

namespace LabAssistant.Data.IO;

/// <summary>
/// Writes text to a file atomically. Content is written to a sibling temporary file and flushed all
/// the way to disk, then swapped into place with <see cref="File.Replace(string, string, string?)"/>
/// (or a move when the destination does not yet exist). This guarantees a crash or power loss during a
/// write can never leave a partially written or truncated destination file: readers always observe
/// either the previous complete file or the new complete file. A single-generation ".bak" backup of the
/// previous good file is retained next to the destination.
/// </summary>
public static class SafeFileWriter
{
    /// <summary>
    /// Atomically writes <paramref name="contents"/> to <paramref name="path"/> using UTF-8 without a BOM.
    /// Creates the parent directory when needed.
    /// </summary>
    public static void WriteAllText(string path, string contents)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        // Fully write and flush the new content to the temp file before touching the destination.
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(contents);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            // Atomic replace that also preserves the prior good file as a one-generation backup.
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}
