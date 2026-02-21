using System;

namespace LabAssistant.ViewModels;

public sealed class ErrorFeedItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? VmName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime TimestampLocal { get; init; } = DateTime.Now;
    public DateTime ExpiresAtUtc { get; init; } = DateTime.UtcNow.AddSeconds(8);
    public bool IsHovered { get; set; }
    public Action? ViewDetailsAction { get; init; }
}
