using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using TestGrid.Changes;
using TestGrid.Data;

namespace TestGrid.ViewModels;

public class ItemsViewModel : ObservableObject
{
    private readonly IUiDispatcher dispatcher;
    private readonly ILogger<ItemsViewModel>? logger;

    public ItemsViewModel(
        GridViewModel grid,
        ChangeBatcher? batcher = null,
        IUiDispatcher? dispatcher = null,
        ILogger<ItemsViewModel>? logger = null)
    {
        Grid = grid;
        this.dispatcher = dispatcher ?? new ImmediateUiDispatcher();
        this.logger = logger;

        Grid.Filter = item => item.Quantity > PcPartCriteria.QuantityThreshold
            && item.Name.StartsWith(PcPartCriteria.NamePrefix);
        Grid.Sort = new ModifiedOnDescendingIdAscendingSort();
        Grid.PageSize = VisibleRows;

        if (batcher is not null)
        {
            batcher.BatchReady += OnBatchReady;
        }
    }

    public int VisibleRows { get; } = 20;

    public GridViewModel Grid { get; }

    public string ConditionsDescription =>
        "Filter: Quantity > 10 and Name starts with \"pc part\". Sort: Modified on (newest first), then Id.";

    private async void OnBatchReady(ChangeBatch batch)
    {
        try
        {
            await dispatcher.InvokeAsync(() => Grid.ApplyChangesAsync(batch));
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Applying a change batch to the grid failed");
        }
    }
}
