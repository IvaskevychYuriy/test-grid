namespace TestGrid.Tests;

public class GridReconcileTests
{
    [Fact]
    public async Task InsertAtHeadInLiveModeIsAddedAtTheTop()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = await fixture.AddNewestAsync();

        await fixture.ApplyAsync(inserted: [id]);

        Assert.Equal(id, fixture.Grid.Items[0].Id);
        Assert.Equal(21, fixture.Grid.Items.Count);
        Assert.True(fixture.Grid.Items[0].IsHighlighted);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task InsertAtHeadWhileBrowsingIsIgnored()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        await fixture.Grid.OnViewportChangedAsync(3, 22);
        Assert.True(fixture.Grid.IsHeadFrozen);
        var before = fixture.Grid.Items.Select(item => item.Id).ToList();
        var id = await fixture.AddNewestAsync();

        await fixture.ApplyAsync(inserted: [id]);

        Assert.Equal(before, fixture.Grid.Items.Select(item => item.Id));
        Assert.Contains(fixture.Grid.ChangeLog, line => line.Contains("ignored 1 changes"));
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task InsertInsideTheLoadedRangeIsPlacedInSortOrder()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        await fixture.Grid.OnViewportChangedAsync(3, 22);
        var between = TestData.BaseModifiedOn.AddSeconds(99).AddMilliseconds(-500);
        var id = await fixture.AddAtAsync(between);

        await fixture.ApplyAsync(inserted: [id]);

        Assert.Equal(1, fixture.Grid.Items.Select(item => item.Id).ToList().IndexOf(id));
        Assert.Equal(ordered[0], fixture.Grid.Items[0].Id);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task InsertBelowTheWindowIsIgnoredWhileMoreRowsFollow()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = await fixture.AddAtAsync(TestData.BaseModifiedOn.AddSeconds(-50));

        await fixture.ApplyAsync(inserted: [id]);

        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == id);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task InsertBelowTheLastRowIsAppendedWhenTheTailIsLoaded()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(10);
        await fixture.Grid.LoadAsync();
        Assert.False(fixture.Grid.HasMoreAfter);
        var id = await fixture.AddAtAsync(TestData.BaseModifiedOn.AddSeconds(-50));

        await fixture.ApplyAsync(inserted: [id]);

        Assert.Equal(id, fixture.Grid.Items[^1].Id);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task InsertNotMatchingTheFilterIsIgnored()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = await fixture.AddNewestAsync(quantity: 3);

        await fixture.ApplyAsync(inserted: [id]);

        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == id);
        Assert.Equal(20, fixture.Grid.Items.Count);
    }

    [Fact]
    public async Task UpdateKeepingModifiedOnIsPatchedInPlace()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = ordered[5];
        await fixture.UpdateAsync(id, item => item.Quantity = 77);

        await fixture.ApplyAsync(updated: [id]);

        Assert.Equal(id, fixture.Grid.Items[5].Id);
        Assert.Equal(77, fixture.Grid.Items[5].Quantity);
        Assert.True(fixture.Grid.Items[5].IsHighlighted);
        Assert.Contains(fixture.Grid.ChangeLog, line => line.Contains("patched in place"));
    }

    [Fact]
    public async Task UpdateMovingToTheTopInLiveModeMovesTheRow()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = ordered[5];
        await fixture.UpdateAsync(
            id,
            item => item.ModifiedOn = TestData.BaseModifiedOn.AddSeconds(fixture.NextNewestSecond()));

        await fixture.ApplyAsync(updated: [id]);

        Assert.Equal(id, fixture.Grid.Items[0].Id);
        Assert.Equal(20, fixture.Grid.Items.Count);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task UpdateMovingAboveTheWindowWhileBrowsingRemovesAndRefills()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        await fixture.Grid.OnViewportChangedAsync(3, 22);
        var count = fixture.Grid.Items.Count;
        var id = fixture.Grid.Items[8].Id;
        await fixture.UpdateAsync(
            id,
            item => item.ModifiedOn = TestData.BaseModifiedOn.AddSeconds(fixture.NextNewestSecond()));

        await fixture.ApplyAsync(updated: [id]);

        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == id);
        Assert.Equal(count, fixture.Grid.Items.Count);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task UpdateThatStopsMatchingTheFilterRemovesTheRow()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = ordered[2];
        await fixture.UpdateAsync(id, item => item.Quantity = 1);

        await fixture.ApplyAsync(updated: [id]);

        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == id);
        Assert.Equal(20, fixture.Grid.Items.Count);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task UpdateThatStartsMatchingTheFilterInsertsTheRow()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        var id = await fixture.AddAtAsync(TestData.BaseModifiedOn.AddSeconds(95).AddMilliseconds(-500), quantity: 1);
        await fixture.Grid.LoadAsync();
        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == id);
        await fixture.UpdateAsync(id, item => item.Quantity = 50);

        await fixture.ApplyAsync(updated: [id]);

        Assert.Contains(fixture.Grid.Items, item => item.Id == id);
        Assert.Equal(ordered[0], fixture.Grid.Items[0].Id);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task DeleteInsideTheWindowRemovesTheRowAndRefills()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        await fixture.DeleteAsync(ordered[4]);

        await fixture.ApplyAsync(deleted: [ordered[4]]);

        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == ordered[4]);
        Assert.Equal(20, fixture.Grid.Items.Count);
        Assert.Contains(fixture.Grid.ChangeLog, line => line.Contains("refilled 1 rows"));
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task DeleteOutsideTheWindowIsIgnored()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var before = fixture.Grid.Items.Select(item => item.Id).ToList();
        await fixture.DeleteAsync(ordered[60]);

        await fixture.ApplyAsync(deleted: [ordered[60]]);

        Assert.Equal(before, fixture.Grid.Items.Select(item => item.Id));
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task DeletingEveryLoadedRowReloadsFromTheHead()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(60);
        await fixture.Grid.LoadAsync();
        var deleted = ordered.Take(20).ToList();
        foreach (var id in deleted)
        {
            await fixture.DeleteAsync(id);
        }

        await fixture.ApplyAsync(deleted: deleted);

        Assert.Equal(20, fixture.Grid.Items.Count);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task ManyHeadInsertsAreTrimmedToTheWindowCap()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var ids = new List<Guid>();
        for (var index = 0; index < 30; index++)
        {
            ids.Add(await fixture.AddNewestAsync());
        }

        await fixture.ApplyAsync(inserted: ids);

        Assert.Equal(fixture.Grid.MaxLoadedRows, fixture.Grid.Items.Count);
        Assert.True(fixture.Grid.HasMoreAfter);
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task ReturningToTheTopRefreshesTheHeadAndResumesLiveMode()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        await fixture.Grid.OnViewportChangedAsync(3, 12);
        Assert.True(fixture.Grid.IsHeadFrozen);
        var id = await fixture.AddNewestAsync();
        await fixture.ApplyAsync(inserted: [id]);
        Assert.DoesNotContain(fixture.Grid.Items, item => item.Id == id);

        await fixture.Grid.OnViewportChangedAsync(0, 9);

        Assert.False(fixture.Grid.IsHeadFrozen);
        Assert.Equal(id, fixture.Grid.Items[0].Id);
        Assert.Contains(fixture.Grid.ChangeLog, line => line.Contains("head refreshed"));
        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Fact]
    public async Task HighlightIsClearedAfterOneSecond()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = await fixture.AddNewestAsync();
        await fixture.ApplyAsync(inserted: [id]);
        var row = fixture.Grid.Items[0];
        Assert.True(row.IsHighlighted);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.True(row.IsHighlighted);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(row.IsHighlighted);
    }

    [Fact]
    public async Task ARenewedHighlightIsNotClearedByAnEarlierTimer()
    {
        using var fixture = new LiveFixture();
        var ordered = await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = ordered[3];
        await fixture.UpdateAsync(id, item => item.Quantity = 31);
        await fixture.ApplyAsync(updated: [id]);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(600));
        await fixture.UpdateAsync(id, item => item.Quantity = 32);
        await fixture.ApplyAsync(updated: [id]);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(500));

        Assert.True(fixture.Grid.Items[3].IsHighlighted);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.False(fixture.Grid.Items[3].IsHighlighted);
    }

    [Fact]
    public async Task EveryBatchWritesASummaryLineAtTheTopOfTheLog()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(100);
        await fixture.Grid.LoadAsync();
        var id = await fixture.AddNewestAsync();

        await fixture.ApplyAsync(inserted: [id]);

        Assert.Contains("batch: 1 events -> 1 inserts, 0 updates, 0 deletes", fixture.Grid.ChangeLog[0]);
        Assert.Contains(fixture.Grid.ChangeLog, line => line.Contains("added at row 0"));
    }

    [Fact]
    public async Task ChangeLogIsCapped()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(30);
        await fixture.Grid.LoadAsync();

        for (var batch = 0; batch < 250; batch++)
        {
            await fixture.ApplyAsync(deleted: [Guid.NewGuid()]);
        }

        Assert.Equal(200, fixture.Grid.ChangeLog.Count);
    }

    [Fact]
    public async Task ReconcileAndScrollLoadingNeverInterleave()
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(200);
        await fixture.Grid.LoadAsync();
        var ids = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            ids.Add(await fixture.AddNewestAsync());
        }

        var scroll = fixture.Grid.OnViewportChangedAsync(10, 19);
        var apply = fixture.ApplyAsync(inserted: ids);
        await Task.WhenAll(scroll, apply);
        await fixture.Grid.OnViewportChangedAsync(0, 9);

        await fixture.AssertWindowMatchesDatabaseAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task WindowAlwaysMatchesAFreshQueryAfterRandomBatches(int seed)
    {
        using var fixture = new LiveFixture();
        await fixture.SeedAsync(150);
        await fixture.Grid.LoadAsync();
        var random = new Random(seed);

        for (var round = 0; round < 25; round++)
        {
            var count = fixture.Grid.Items.Count;
            if (count > 0 && random.Next(3) == 0)
            {
                var first = random.Next(0, Math.Max(1, count - 19));
                await fixture.Grid.OnViewportChangedAsync(first, Math.Min(count - 1, first + 19));
            }

            var all = await fixture.ExpectedOrderAsync();
            var inserted = new List<Guid>();
            var updated = new List<Guid>();
            var deleted = new List<Guid>();

            for (var change = 0; change < 6 && all.Count > 5; change++)
            {
                var target = all[random.Next(all.Count)];
                switch (random.Next(4))
                {
                    case 0:
                        inserted.Add(await fixture.AddNewestAsync());
                        break;
                    case 1:
                        if (!deleted.Contains(target))
                        {
                            updated.Add(target);
                            await fixture.UpdateAsync(target, item =>
                            {
                                item.Quantity = random.Next(2) == 0 ? 1 : 40;
                                if (random.Next(2) == 0)
                                {
                                    item.ModifiedOn = TestData.BaseModifiedOn.AddSeconds(fixture.NextNewestSecond());
                                }
                            });
                        }

                        break;
                    case 2:
                        if (!deleted.Contains(target))
                        {
                            deleted.Add(target);
                            await fixture.DeleteAsync(target);
                        }

                        break;
                    default:
                        var offset = TimeSpan.FromMilliseconds((random.Next(-30, 150) * 1000) + random.Next(1000));
                        inserted.Add(await fixture.AddAtAsync(TestData.BaseModifiedOn + offset));
                        break;
                }

                all = await fixture.ExpectedOrderAsync();
            }

            await fixture.ApplyAsync(inserted, updated.Where(id => !deleted.Contains(id)), deleted);

            await fixture.AssertWindowMatchesDatabaseAsync();
        }
    }
}
