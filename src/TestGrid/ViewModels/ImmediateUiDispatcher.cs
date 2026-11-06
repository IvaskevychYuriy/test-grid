namespace TestGrid.ViewModels;

public class ImmediateUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Func<Task> action) => action();

    public void Post(Action action) => action();
}
