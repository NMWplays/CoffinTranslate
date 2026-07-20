using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoffinTranslate.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GameService _gameService;

    public MainWindowViewModel(GameService gameService, SettingsService settings, IFilePickerService filePicker)
    {
        _gameService = gameService;
        Install = new InstallViewModel(gameService, filePicker);
        Manage = new ManageViewModel(gameService);
        Editor = new EditorViewModel(gameService, filePicker, settings);
        Settings = new SettingsViewModel(gameService, settings, filePicker);

        Install.Installed += (_, _) => _ = Manage.RefreshAsync();
    }

    public InstallViewModel Install { get; }

    public ManageViewModel Manage { get; }

    public EditorViewModel Editor { get; }

    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    public partial int SelectedPageIndex { get; set; }

    /// <summary>The view model for the currently selected page; the shell hosts it in a
    /// <c>TransitioningContentControl</c> so switching pages cross-fades.</summary>
    public object CurrentPage => SelectedPageIndex switch
    {
        1 => Manage,
        2 => Editor,
        3 => Settings,
        _ => Install,
    };

    partial void OnSelectedPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPage));

        if (value == 0)
            _ = Install.RefreshCommunityStatesAsync();
        else if (value == 1)
            _ = Manage.RefreshAsync();
    }

    public Task InitializeAsync() => _gameService.DetectAsync();
}
