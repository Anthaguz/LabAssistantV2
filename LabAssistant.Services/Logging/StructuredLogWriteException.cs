namespace LabAssistant.Services.Logging;

public sealed class StructuredLogWriteException : Exception
{
    public StructuredLogWriteException(string filePath, string message, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}
