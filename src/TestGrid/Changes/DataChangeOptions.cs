namespace TestGrid.Changes;

public class DataChangeOptions
{
    public int ChangesPerType { get; init; } = 2;

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan BatchWindow { get; init; } = TimeSpan.FromSeconds(2);
}
