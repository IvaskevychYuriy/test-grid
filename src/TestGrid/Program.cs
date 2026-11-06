using Avalonia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestGrid.Data;
using TestGrid.ViewModels;

namespace TestGrid;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        ConfigureServices(builder.Services, GetDatabasePath());

        using var host = builder.Build();
        InitializeDatabaseAsync(host.Services).GetAwaiter().GetResult();

        BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
    }

    public static void ConfigureServices(IServiceCollection services, string databasePath)
    {
        services.AddSingleton<ModifiedOnInterceptor>();
        services.AddDbContextFactory<AppDbContext>((provider, options) => options
            .UseSqlite($"Data Source={databasePath}")
            .AddInterceptors(provider.GetRequiredService<ModifiedOnInterceptor>()));
        services.AddSingleton<DbSeeder>();
        services.AddTransient<GridViewModel>();
        services.AddTransient<ItemsViewModel>();
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.MigrateAsync();
        }

        await services.GetRequiredService<DbSeeder>().SeedIfEmptyAsync();
    }

    private static string GetDatabasePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestGrid");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "items.db");
    }

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect();
}
