using System.Collections.ObjectModel;
using CoffinTranslate.Core.Install;
using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoffinTranslate.ViewModels;

public partial class ManageViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly TranslationInstaller _installer = new();

    private static Localizer L => Localizer.Instance;

    public ManageViewModel(GameService gameService)
    {
        _gameService = gameService;
        _gameService.PropertyChanged += (_, _) => _ = RefreshAsync();
        L.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
            foreach (var item in Items)
                item.RefreshTexts();
        };
    }

    public ObservableCollection<InstalledItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool HasGame => _gameService.Game is not null;

    public bool CanLaunchGame => _gameService.CanLaunch;

    public bool IsEmpty => HasGame && Items.Count == 0 && !IsBusy;

    [RelayCommand]
    private void LaunchGame() => _gameService.LaunchGame();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Items.Clear();
        StatusMessage = null;
        OnPropertyChanged(nameof(HasGame));
        OnPropertyChanged(nameof(CanLaunchGame));

        if (_gameService.Game is not { } game)
        {
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        IsBusy = true;
        try
        {
            var installed = await Task.Run(() => InstalledTranslationScanner.Scan(game));
            foreach (var translation in installed)
                Items.Add(new InstalledItemViewModel(translation, this));
        }
        catch (Exception ex)
        {
            StatusMessage = L.Format("Error_InstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    internal async Task RemoveAsync(InstalledItemViewModel item)
    {
        string? message;
        try
        {
            var backupPath = await Task.Run(() => _installer.Uninstall(item.Model));
            message = backupPath is null ? null : L.Format("Manage_RemovedNote", item.DisplayLanguage, backupPath);
        }
        catch (Exception ex)
        {
            message = L.Format("Error_InstallFailed", ex.Message);
        }

        await RefreshAsync();
        StatusMessage = message;
    }
}
