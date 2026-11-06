namespace TestGrid.Changes;

public class DataChangedEventArgs(DataChangeType changeType, Guid itemId, ItemPatch? patch) : EventArgs
{
    public DataChangeType ChangeType { get; } = changeType;

    public Guid ItemId { get; } = itemId;

    public ItemPatch? Patch { get; } = patch;
}
