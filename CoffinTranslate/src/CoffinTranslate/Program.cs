using Avalonia;
using System;

namespace CoffinTranslate;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Render popups (ComboBox drop-downs, flyouts) inside the main window's overlay
            // layer instead of as separate OS windows. Avoids the "PlatformImpl is null,
            // couldn't handle input" popup-window issue that swallowed drop-down clicks.
            .With(new Win32PlatformOptions { OverlayPopups = true })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
