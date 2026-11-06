using Avalonia.Threading;

namespace TestGrid.ViewModels;

public class AvaloniaUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Func<Task> action) => Dispatcher.UIThread.InvokeAsync(action);

    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
