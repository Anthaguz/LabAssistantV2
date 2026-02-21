using System;
using System.Collections.ObjectModel;

namespace LabAssistant.ViewModels;

public interface IErrorFeedService
{
    ObservableCollection<ErrorFeedItem> ActiveItems { get; }
    ObservableCollection<ErrorFeedItem> RecentItems { get; }
    int TotalErrorCount { get; }
    event Action? StateChanged;

    void Publish(string? vmName, string title, string message, Action? viewDetailsAction = null);
    void Dismiss(Guid id);
}
