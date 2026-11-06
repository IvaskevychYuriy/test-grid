namespace TestGrid.Changes;

public class DataChangeNotifier : IDataChangeNotifier
{
    public event EventHandler<DataChangedEventArgs>? DataChanged;

    public void Publish(DataChangedEventArgs change) => DataChanged?.Invoke(this, change);
}
