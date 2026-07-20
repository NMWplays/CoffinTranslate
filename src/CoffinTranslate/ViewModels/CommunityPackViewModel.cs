using CoffinTranslate.Core.Community;
using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoffinTranslate.ViewModels;

/// <summary>One row in the community catalog: a translation the user can download and install with
/// one click. State (not installed / installed / update available) and download progress live here.</summary>
public partial class CommunityPackViewModel : ViewModelBase
{
    private readonly Func<CommunityPackViewModel, Task> _install;
    private string? _gameMismatchVersion;

    private static Localizer L => Localizer.Instance;

    public CommunityPackViewModel(CommunityPack pack, Func<CommunityPackViewModel, Task> install)
    {
        Pack = pack;
        _install = install;
        L.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    public CommunityPack Pack { get; }

    [ObservableProperty]
    public partial PackInstallState State { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsError { get; set; }

    public string Language => Pack.Language;

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (Pack.Authors.Length > 0)
                parts.Add(L.Format("Community_By", Pack.Authors));
            parts.Add("v" + Pack.Version);
            return string.Join("  ·  ", parts);
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Pack.Description);

    public string Description => Pack.Description ?? "";

    public bool HasSource => Pack.Source is { Length: > 0 };

    public bool ShowGameVersion => Pack.GameVersion is { Length: > 0 };

    public string GameVersionText => L.Format("Community_ForGameVersion", Pack.GameVersion ?? "");

    public bool ShowMismatch => _gameMismatchVersion is not null;

    public string MismatchText => L.Format("Community_VersionMismatch", _gameMismatchVersion ?? "");

    public bool IsInstalled => State == PackInstallState.Installed;

    public bool HasUpdate => State == PackInstallState.UpdateAvailable;

    public string ActionText => State switch
    {
        PackInstallState.UpdateAvailable => L["Community_Update"],
        PackInstallState.Installed => L["Community_Reinstall"],
        _ => L["Community_Install"],
    };

    public bool HasStatus => StatusMessage is { Length: > 0 };

    public bool ShowSuccessStatus => HasStatus && !IsError;

    public bool ShowErrorStatus => HasStatus && IsError;

    [RelayCommand]
    private Task Install() => _install(this);

    [RelayCommand]
    private void OpenSource()
    {
        if (Pack.Source is { Length: > 0 } url)
            ExplorerService.OpenUrl(url);
    }

    /// <summary>Sets the current version of the installed game, so a mismatch hint can be shown.</summary>
    public void SetGameMismatch(string? currentGameVersion)
    {
        _gameMismatchVersion = currentGameVersion;
        OnPropertyChanged(nameof(ShowMismatch));
        OnPropertyChanged(nameof(MismatchText));
    }

    public void SetStatus(string? message, bool isError)
    {
        IsError = isError;
        StatusMessage = message;
    }

    partial void OnStateChanged(PackInstallState value) => OnPropertyChanged(string.Empty);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(string.Empty);

    partial void OnStatusMessageChanged(string? value) => RaiseStatusFlags();

    partial void OnIsErrorChanged(bool value) => RaiseStatusFlags();

    private void RaiseStatusFlags()
    {
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(ShowSuccessStatus));
        OnPropertyChanged(nameof(ShowErrorStatus));
    }
}
