using System.Reflection;
using Avalonia.Threading;
using CoffinTranslate.Core.Install;
using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoffinTranslate.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly SettingsService _settings;
    private readonly IFilePickerService _filePicker;

    private static Localizer L => Localizer.Instance;

    public SettingsViewModel(GameService gameService, SettingsService settings, IFilePickerService filePicker)
    {
        _gameService = gameService;
        _settings = settings;
        _filePicker = filePicker;
        SelectedLanguage = Localizer.SupportedLanguages
            .FirstOrDefault(l => l.Code == Localizer.Instance.CurrentLanguage) ?? Localizer.SupportedLanguages[0];
        ThemeCode = ThemeService.Normalize(settings.Current.Theme);
        AutosaveSeconds = settings.Current.AutosaveSeconds;
        DefaultFontFace = settings.Current.DefaultFontFace ?? "";

        _gameService.PropertyChanged += (_, _) => OnPropertyChanged(nameof(GamePathText));
        L.PropertyChanged += (_, _) => OnPropertyChanged(nameof(GamePathText));
    }

    public IReadOnlyList<UiLanguage> Languages => Localizer.SupportedLanguages;

    [ObservableProperty]
    public partial UiLanguage SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial bool IsLanguageOpen { get; set; }

    [RelayCommand]
    private void ToggleLanguage() => IsLanguageOpen = !IsLanguageOpen;

    // Picks a language from the dropdown and always closes it — even when the current
    // language is re-selected (which doesn't raise a SelectedLanguage change).
    [RelayCommand]
    private void SelectLanguage(UiLanguage language)
    {
        IsLanguageOpen = false;
        SelectedLanguage = language;
    }

    // --- theme ---

    [ObservableProperty]
    public partial string ThemeCode { get; set; } = ThemeService.System;

    public bool IsThemeSystem => ThemeCode == ThemeService.System;

    public bool IsThemeLight => ThemeCode == ThemeService.Light;

    public bool IsThemeDark => ThemeCode == ThemeService.Dark;

    [RelayCommand]
    private void SetTheme(string code) => ThemeCode = ThemeService.Normalize(code);

    partial void OnThemeCodeChanged(string value)
    {
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));

        ThemeService.Apply(value);
        _settings.Current.Theme = value == ThemeService.System ? null : value;
        _settings.Save();
    }

    // --- editor preferences ---

    [ObservableProperty]
    public partial int AutosaveSeconds { get; set; }

    [ObservableProperty]
    public partial string DefaultFontFace { get; set; } = "";

    partial void OnAutosaveSecondsChanged(int value)
    {
        var clamped = value < 1 ? 1 : value > 600 ? 600 : value;
        if (clamped != value)
        {
            AutosaveSeconds = clamped;
            return;
        }

        _settings.Current.AutosaveSeconds = clamped;
        _settings.Save();
    }

    partial void OnDefaultFontFaceChanged(string value)
    {
        _settings.Current.DefaultFontFace = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _settings.Save();
    }

    [ObservableProperty]
    public partial string? GameError { get; set; }

    public bool HasGameError => GameError is not null;

    public string GamePathText => _gameService.Game?.RootPath ?? L["Settings_NotSet"];

    public string BackupPathText => TranslationInstaller.DefaultBackupRoot;

    public string VersionText =>
        "CoffinTranslate v" + (Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev");

    partial void OnSelectedLanguageChanged(UiLanguage value)
    {
        IsLanguageOpen = false;

        if (value is null || value.Code == Localizer.Instance.CurrentLanguage)
            return;

        // Persist immediately, but apply the language change (a broad, synchronous UI refresh)
        // after the ComboBox has finished its own selection/popup handling. Doing it inline would
        // re-enter layout while input is still being processed, which can throw.
        _settings.Current.UiLanguage = value.Code;
        _settings.Save();

        var code = value.Code;
        Dispatcher.UIThread.Post(() => Localizer.Instance.SetLanguage(code), DispatcherPriority.Background);
    }

    partial void OnGameErrorChanged(string? value) => OnPropertyChanged(nameof(HasGameError));

    [RelayCommand]
    private async Task ChangeGamePathAsync()
    {
        var path = await _filePicker.PickFolderAsync(L["Install_SelectGameFolder"]);
        if (path is null)
            return;

        GameError = _gameService.TrySetManualPath(path) ? null : L["Install_InvalidGameFolder"];
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        Directory.CreateDirectory(TranslationInstaller.DefaultBackupRoot);
        ExplorerService.OpenFolder(TranslationInstaller.DefaultBackupRoot);
    }
}
