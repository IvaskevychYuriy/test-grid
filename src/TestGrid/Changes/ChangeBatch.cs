namespace TestGrid.Changes;

public class ChangeBatch
{
    public ChangeBatch(
        int eventCount,
        IReadOnlySet<Guid> inserted,
        IReadOnlySet<Guid> updated,
        IReadOnlySet<Guid> deleted)
    {
        EventCount = eventCount;
        Inserted = inserted;
        Updated = updated;
        Deleted = deleted;
    }

    public int EventCount { get; }

    public IReadOnlySet<Guid> Inserted { get; }

    public IReadOnlySet<Guid> Updated { get; }

    public IReadOnlySet<Guid> Deleted { get; }

    public IEnumerable<Guid> Upserted => Inserted.Concat(Updated);

    public static ChangeBatch Aggregate(IReadOnlyCollection<DataChangedEventArgs> events)
    {
        var netEffects = new Dictionary<Guid, NetEffect>();
        foreach (var change in events)
        {
            var previous = netEffects.GetValueOrDefault(change.ItemId, NetEffect.Unknown);
            netEffects[change.ItemId] = Combine(previous, change.ChangeType);
        }

        var inserted = new HashSet<Guid>();
        var updated = new HashSet<Guid>();
        var deleted = new HashSet<Guid>();
        foreach (var (id, effect) in netEffects)
        {
            switch (effect)
            {
                case NetEffect.Insert:
                    inserted.Add(id);
                    break;
                case NetEffect.Update:
                    updated.Add(id);
                    break;
                case NetEffect.Delete:
                    deleted.Add(id);
                    break;
            }
        }

        return new ChangeBatch(events.Count, inserted, updated, deleted);
    }

    private static NetEffect Combine(NetEffect previous, DataChangeType next) => (previous, next) switch
    {
        (NetEffect.Unknown, DataChangeType.Insert) => NetEffect.Insert,
        (NetEffect.Unknown, DataChangeType.Update) => NetEffect.Update,
        (NetEffect.Unknown, DataChangeType.Delete) => NetEffect.Delete,
        (NetEffect.Insert, DataChangeType.Delete) => NetEffect.Cancelled,
        (NetEffect.Update, DataChangeType.Delete) => NetEffect.Delete,
        _ => previous,
    };

    private enum NetEffect
    {
        Unknown,
        Insert,
        Update,
        Delete,
        Cancelled,
    }
}
