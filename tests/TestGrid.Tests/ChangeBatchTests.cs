using Microsoft.Extensions.Time.Testing;
using TestGrid.Changes;

namespace TestGrid.Tests;

public class ChangeBatchTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void InsertThenUpdateIsInsert()
    {
        var batch = ChangeBatch.Aggregate([Change(DataChangeType.Insert, A), Change(DataChangeType.Update, A)]);

        Assert.Equal([A], batch.Inserted);
        Assert.Empty(batch.Updated);
        Assert.Empty(batch.Deleted);
        Assert.Equal(2, batch.EventCount);
    }

    [Fact]
    public void InsertThenDeleteCancelsOut()
    {
        var batch = ChangeBatch.Aggregate([Change(DataChangeType.Insert, A), Change(DataChangeType.Delete, A)]);

        Assert.Empty(batch.Inserted);
        Assert.Empty(batch.Updated);
        Assert.Empty(batch.Deleted);
    }

    [Fact]
    public void UpdateThenDeleteIsDelete()
    {
        var batch = ChangeBatch.Aggregate([Change(DataChangeType.Update, A), Change(DataChangeType.Delete, A)]);

        Assert.Equal([A], batch.Deleted);
        Assert.Empty(batch.Updated);
    }

    [Fact]
    public void RepeatedUpdatesAreOneUpdate()
    {
        var batch = ChangeBatch.Aggregate(
            [Change(DataChangeType.Update, A), Change(DataChangeType.Update, A), Change(DataChangeType.Update, A)]);

        Assert.Equal([A], batch.Updated);
        Assert.Equal(3, batch.EventCount);
    }

    [Fact]
    public void DifferentItemsAreKeptSeparate()
    {
        var batch = ChangeBatch.Aggregate(
            [Change(DataChangeType.Insert, A), Change(DataChangeType.Update, B), Change(DataChangeType.Delete, C)]);

        Assert.Equal([A], batch.Inserted);
        Assert.Equal([B], batch.Updated);
        Assert.Equal([C], batch.Deleted);
        Assert.Equal(new[] { A, B }, batch.Upserted.ToArray());
    }

    [Fact]
    public void BatcherDoesNothingWithoutEvents()
    {
        var (_, time, batches) = CreateBatcher();

        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Empty(batches);
    }

    [Fact]
    public void BatcherFlushesOneBatchWhenWindowExpires()
    {
        var (notifier, time, batches) = CreateBatcher();

        notifier.Publish(Change(DataChangeType.Insert, A));
        time.Advance(TimeSpan.FromSeconds(1));
        notifier.Publish(Change(DataChangeType.Update, B));
        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Empty(batches);
        time.Advance(TimeSpan.FromMilliseconds(1));

        var batch = Assert.Single(batches);
        Assert.Equal(2, batch.EventCount);
        Assert.Equal([A], batch.Inserted);
        Assert.Equal([B], batch.Updated);
    }

    [Fact]
    public void BatcherWindowDoesNotResetAndNextWindowStartsAfterFlush()
    {
        var (notifier, time, batches) = CreateBatcher();

        notifier.Publish(Change(DataChangeType.Insert, A));
        for (var second = 0; second < 2; second++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            notifier.Publish(Change(DataChangeType.Update, B));
        }

        Assert.Single(batches);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(batches);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, batches.Count);
        Assert.Equal(1, batches[1].EventCount);
    }

    private static DataChangedEventArgs Change(DataChangeType type, Guid id) => new(type, id, null);

    private static (DataChangeNotifier Notifier, FakeTimeProvider Time, List<ChangeBatch> Batches) CreateBatcher()
    {
        var notifier = new DataChangeNotifier();
        var time = new FakeTimeProvider();
        var batcher = new ChangeBatcher(notifier, new DataChangeOptions(), time);
        var batches = new List<ChangeBatch>();
        batcher.BatchReady += batches.Add;
        return (notifier, time, batches);
    }
}
