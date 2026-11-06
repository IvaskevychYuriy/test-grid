namespace TestGrid.Changes;

public interface IDataChangeNotifier
{
    event EventHandler<DataChangedEventArgs>? DataChanged;

    void Publish(DataChangedEventArgs change);
}
