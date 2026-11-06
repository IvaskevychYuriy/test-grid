using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using TestGrid.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace TestGrid.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://TestGrid.Tests"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
