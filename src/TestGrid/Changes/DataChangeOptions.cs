namespace TestGrid.Changes;

public class DataChangeOptions
{
    public int ChangesPerType { get; init; } = 4;

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(1);
}
