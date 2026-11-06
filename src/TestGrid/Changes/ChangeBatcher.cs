namespace TestGrid.Changes;

public sealed class ChangeBatcher : IDisposable
{
    private readonly IDataChangeNotifier notifier;
    private readonly DataChangeOptions options;
    private readonly TimeProvider timeProvider;
    private readonly object gate = new();
    private List<DataChangedEventArgs> pending = [];
    private ITimer? timer;

    public ChangeBatcher(IDataChangeNotifier notifier, DataChangeOptions options, TimeProvider timeProvider)
    {
        this.notifier = notifier;
        this.options = options;
        this.timeProvider = timeProvider;
        notifier.DataChanged += OnDataChanged;
    }

    public event Action<ChangeBatch>? BatchReady;

    public void Dispose()
    {
        notifier.DataChanged -= OnDataChanged;
        lock (gate)
        {
            timer?.Dispose();
            timer = null;
        }
    }

    private void OnDataChanged(object? sender, DataChangedEventArgs change)
    {
        lock (gate)
        {
            pending.Add(change);
            timer ??= timeProvider.CreateTimer(
                _ => Flush(),
                state: null,
                dueTime: options.BatchWindow,
                period: Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        List<DataChangedEventArgs> events;
        lock (gate)
        {
            events = pending;
            pending = [];
            timer?.Dispose();
            timer = null;
        }

        if (events.Count > 0)
        {
            BatchReady?.Invoke(ChangeBatch.Aggregate(events));
        }
    }
}
