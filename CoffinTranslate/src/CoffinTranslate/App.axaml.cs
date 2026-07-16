using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CoffinTranslate.Services;
using CoffinTranslate.ViewModels;
using CoffinTranslate.Views;

namespace CoffinTranslate;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsService();
            Localizer.Instance.SetLanguage(settings.Current.UiLanguage ?? Localizer.DetectSystemLanguage());
            ThemeService.Apply(settings.Current.Theme);

            var gameService = new GameService(settings);
            var mainWindow = new MainWindow();
            var filePicker = new FilePickerService(() => mainWindow);
            var viewModel = new MainWindowViewModel(gameService, settings, filePicker);

            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;

            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
