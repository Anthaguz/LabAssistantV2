using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace LabAssistant.ViewModels;

public sealed class ErrorFeedService : IErrorFeedService
{
    private readonly DispatcherTimer _cleanupTimer;

    public ObservableCollection<ErrorFeedItem> ActiveItems { get; } = new();
    public ObservableCollection<ErrorFeedItem> RecentItems { get; } = new();
    public int TotalErrorCount { get; private set; }
    public event Action? StateChanged;

    public ErrorFeedService()
    {
        _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cleanupTimer.Tick += (_, _) => RemoveExpiredItems();
        _cleanupTimer.Start();
    }

    public void Publish(string? vmName, string title, string message, Action? viewDetailsAction = null)
    {
        var item = new ErrorFeedItem
        {
            VmName = vmName,
            Title = title,
            Message = message,
            TimestampLocal = DateTime.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(8),
            ViewDetailsAction = viewDetailsAction
        };

        RunOnUiThread(() =>
        {
            ActiveItems.Insert(0, item);
            RecentItems.Insert(0, item);
            while (RecentItems.Count > 25)
            {
                RecentItems.RemoveAt(RecentItems.Count - 1);
            }

            TotalErrorCount++;
            StateChanged?.Invoke();
        });
    }

    public void Dismiss(Guid id)
    {
        RunOnUiThread(() =>
        {
            var item = ActiveItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                ActiveItems.Remove(item);
                StateChanged?.Invoke();
            }
        });
    }

    private void RemoveExpiredItems()
    {
        RunOnUiThread(() =>
        {
            var now = DateTime.UtcNow;
            for (var i = ActiveItems.Count - 1; i >= 0; i--)
            {
                var item = ActiveItems[i];
                if (!item.IsHovered && item.ExpiresAtUtc <= now)
                {
                    ActiveItems.RemoveAt(i);
                }
            }
        });
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
