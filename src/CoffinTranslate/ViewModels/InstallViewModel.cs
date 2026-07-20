using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Platform.Storage;
using CoffinTranslate.Core;
using CoffinTranslate.Core.Community;
using CoffinTranslate.Core.Game;
using CoffinTranslate.Core.Install;
using CoffinTranslate.Core.Packages;
using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoffinTranslate.ViewModels;

public partial class InstallViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly IFilePickerService _filePicker;
    private readonly TranslationInstaller _installer = new();
    private readonly CommunityService _community = new();
    private bool _catalogLoaded;

    private static Localizer L => Localizer.Instance;

    public InstallViewModel(GameService gameService, IFilePickerService filePicker)
    {
        _gameService = gameService;
        _filePicker = filePicker;
        _gameService.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
            if (_catalogLoaded)
                _ = RecomputePackStatesAsync();
        };
        L.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);

        // the community catalog is the headline install path — open on it; the mode-change handler
        // kicks off the initial catalog fetch
        CommunityMode = true;
    }

    /// <summary>Raised after a successful install so other pages can refresh.</summary>
    public event EventHandler? Installed;

    [ObservableProperty]
    public partial TranslationPackage? Package { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? GameError { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool InstallSucceeded { get; set; }

    [ObservableProperty]
    public partial string? SuccessDetail { get; set; }

    // --- community catalog ---

    /// <summary>True while the "Community" tab is shown; false for the manual drag&drop/pick flow.</summary>
    [ObservableProperty]
    public partial bool CommunityMode { get; set; }

    [ObservableProperty]
    public partial bool IsCatalogLoading { get; set; }

    [ObservableProperty]
    public partial string? CatalogError { get; set; }

    public ObservableCollection<CommunityPackViewModel> CommunityPacks { get; } = [];

    public bool ManualMode => !CommunityMode;

    public bool HasCatalogError => CatalogError is not null;

    public bool ShowCommunityEmpty =>
        _catalogLoaded && !IsCatalogLoading && CatalogError is null && CommunityPacks.Count == 0;

    [RelayCommand]
    private void ShowCommunity() => CommunityMode = true;

    [RelayCommand]
    private void ShowManual() => CommunityMode = false;

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        if (IsCatalogLoading)
            return;

        IsCatalogLoading = true;
        CatalogError = null;
        try
        {
            var catalog = await _community.FetchCatalogAsync();
            CommunityPacks.Clear();
            foreach (var pack in catalog.Packs)
                CommunityPacks.Add(new CommunityPackViewModel(pack, DownloadAndInstallAsync));
            _catalogLoaded = true;
            await RecomputePackStatesAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or CommunityCatalogException)
        {
            CatalogError = L["Community_LoadError"];
        }
        catch (Exception)
        {
            CatalogError = L["Community_LoadError"];
        }
        finally
        {
            IsCatalogLoading = false;
            OnPropertyChanged(nameof(ShowCommunityEmpty));
        }
    }

    /// <summary>Re-checks which community packs are still installed. Call when the page is shown again,
    /// e.g. after the user removed a translation on the "Installed" page, so a pack that was deleted
    /// there stops showing as installed here.</summary>
    public Task RefreshCommunityStatesAsync() =>
        _catalogLoaded ? RecomputePackStatesAsync() : Task.CompletedTask;

    /// <summary>Re-derives every pack's installed/update state and game-version match from the current
    /// game folder and the local install ledger.</summary>
    private async Task RecomputePackStatesAsync()
    {
        if (CommunityPacks.Count == 0)
            return;

        var installedNames = _gameService.Game is { } game
            ? await Task.Run(() => InstalledTranslationScanner.Scan(game).Select(t => t.Name).ToList())
            : [];
        var currentVersion = VersionPart(await _gameService.GetCurrentVersionHashAsync());

        foreach (var vm in CommunityPacks)
        {
            var record = _community.Ledger.Get(vm.Pack.Id);
            vm.State = CommunityInstall.DetermineState(vm.Pack, record, installedNames);
            vm.SetGameMismatch(MismatchVersion(vm.Pack, currentVersion));

            // clear the transient "Installed!"/error banner from an earlier action; on a page
            // refresh it is stale and, once a pack was removed elsewhere, plainly contradictory
            if (!vm.IsBusy)
                vm.SetStatus(null, isError: false);
        }
    }

    private async Task DownloadAndInstallAsync(CommunityPackViewModel packVm)
    {
        if (_gameService.Game is not { } game)
        {
            packVm.SetStatus(L["Community_NoGame"], isError: true);
            return;
        }

        packVm.IsBusy = true;
        packVm.Progress = 0;
        packVm.SetStatus(null, isError: false);
        try
        {
            var progress = new Progress<double>(p => packVm.Progress = p);
            var zipPath = await _community.DownloadPackAsync(packVm.Pack, progress);
            var package = await Task.Run(() => PackageReader.Read(zipPath));
            var result = await Task.Run(() => _installer.Install(game, package));
            _community.Ledger.Record(packVm.Pack.Id, package.InstallName, packVm.Pack.Version);
            packVm.State = PackInstallState.Installed;
            packVm.SetStatus(
                result.BackupPath is not null ? L["Community_InstalledReplaced"] : L["Community_InstalledDone"],
                isError: false);
            TryDelete(zipPath);
            Installed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            packVm.SetStatus(L["Community_DownloadFailed"], isError: true);
        }
        catch (PackageReadException ex)
        {
            packVm.SetStatus(MapReadError(ex.Error), isError: true);
        }
        catch (InstallException ex)
        {
            packVm.SetStatus(MapInstallError(ex.Code), isError: true);
        }
        catch (Exception ex)
        {
            packVm.SetStatus(L.Format("Error_InstallFailed", ex.Message), isError: true);
        }
        finally
        {
            packVm.IsBusy = false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // leftover temp download is harmless
        }
    }

    /// <summary>The bare version part of a "3.0.13 : &lt;hash&gt;" version string, or null.</summary>
    private static string? VersionPart(string? versionHash)
    {
        if (versionHash is null)
            return null;
        var head = versionHash.Split(':')[0].Trim();
        return head.Length > 0 ? head : null;
    }

    private static string? MismatchVersion(CommunityPack pack, string? currentVersion) =>
        pack.GameVersion is { Length: > 0 } target && currentVersion is { Length: > 0 }
        && !string.Equals(target, currentVersion, StringComparison.OrdinalIgnoreCase)
            ? currentVersion
            : null;

    // --- game section ---

    public bool GameFound => _gameService.Game is not null;

    public string GameStatusTitle => _gameService.Game switch
    {
        null => L["Install_GameNotFound_Title"],
        { Source: GameSource.Steam } => L["Install_GameFound_Steam"],
        _ => L["Install_GameFound_Manual"],
    };

    public string GameStatusDetail => _gameService.Game?.RootPath ?? L["Install_GameNotFound_Hint"];

    public bool HasGameError => GameError is not null;

    /// <summary>Whether a "Launch game" button should be shown (a Game.exe is present).</summary>
    public bool CanLaunchGame => _gameService.CanLaunch;

    // --- package section ---

    public bool HasPackage => Package is not null;

    public bool ShowDropZone => !HasPackage && !InstallSucceeded;

    public bool ShowPackage => HasPackage && !InstallSucceeded;

    public bool CanInstall => GameFound && HasPackage && !IsBusy;

    public bool HasError => ErrorMessage is not null;

    public bool HasSuccessDetail => SuccessDetail is not null;

    public string PackageTitle => Package?.DisplayLanguage ?? "";

    public string PackageCredits =>
        Package?.Metadata?.Credits is { Count: > 0 } credits ? string.Join(" · ", credits) : "";

    public bool HasPackageCredits => PackageCredits.Length > 0;

    public string PackageSource =>
        Package is null
            ? ""
            : Path.GetFileName(Package.SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public string PackageContents
    {
        get
        {
            if (Package is null)
                return "";

            var parts = new List<string>
            {
                Package.Format switch
                {
                    DialogueFormat.Cld => L["Install_Content_Cld"],
                    DialogueFormat.Csv => L.Format("Install_Content_Dialogue", "dialogue.csv"),
                    _ => L.Format("Install_Content_Dialogue", "dialogue.txt"),
                },
            };
            if (Package.ImageCount > 0)
                parts.Add(L.Format("Install_Content_Images", Package.ImageCount));
            if (Package.HasFont)
                parts.Add(L["Install_Content_Font"]);
            return string.Join(" · ", parts);
        }
    }

    public string PackageWarnings =>
        Package is null
            ? ""
            : string.Join(Environment.NewLine, Package.Warnings.Select(w => w.Code switch
            {
                PackageWarningCode.MultipleTranslationsFound => L["Warning_MultipleTranslations"],
                PackageWarningCode.EncodingProblems => L["Warning_Encoding"],
                _ => w.Code.ToString(),
            }));

    public bool HasPackageWarnings => Package?.Warnings.Count > 0;

    public string ReplaceWarning
    {
        get
        {
            if (_gameService.Game is not { } game || Package is not { } package)
                return "";

            var target = Path.Combine(game.LanguagesPath, package.InstallName);
            return File.Exists(target) || Directory.Exists(target)
                ? L.Format("Install_ReplaceWarning", package.InstallName)
                : "";
        }
    }

    public bool HasReplaceWarning => ReplaceWarning.Length > 0;

    // --- commands ---

    [RelayCommand]
    private async Task BrowseGameFolderAsync()
    {
        var path = await _filePicker.PickFolderAsync(L["Install_SelectGameFolder"]);
        if (path is null)
            return;

        GameError = _gameService.TrySetManualPath(path) ? null : L["Install_InvalidGameFolder"];
    }

    [RelayCommand]
    private void LaunchGame() => _gameService.LaunchGame();

    [RelayCommand]
    private async Task BrowsePackageFileAsync()
    {
        var fileTypes = new List<FilePickerFileType>
        {
            new(L["Install_FileTypeName"]) { Patterns = ["*.zip", "*.cld", "dialogue.txt", "dialogue.csv"] },
            FilePickerFileTypes.All,
        };
        var path = await _filePicker.PickFileAsync(L["Install_PickPackageFile"], fileTypes);
        if (path is not null)
            await LoadPackageAsync(path);
    }

    [RelayCommand]
    private async Task BrowsePackageFolderAsync()
    {
        var path = await _filePicker.PickFolderAsync(L["Install_PickPackageFolder"]);
        if (path is not null)
            await LoadPackageAsync(path);
    }

    [RelayCommand]
    private void ClearPackage()
    {
        Package = null;
        ErrorMessage = null;
    }

    public async Task LoadPackageAsync(string path)
    {
        IsBusy = true;
        ErrorMessage = null;
        InstallSucceeded = false;
        try
        {
            Package = await Task.Run(() => PackageReader.Read(path));
        }
        catch (PackageReadException ex)
        {
            Package = null;
            ErrorMessage = MapReadError(ex.Error);
        }
        catch (Exception ex)
        {
            Package = null;
            ErrorMessage = L.Format("Error_InstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (_gameService.Game is not { } game || Package is not { } package)
            return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await Task.Run(() => _installer.Install(game, package));
            SuccessDetail = result.BackupPath is not null ? L["Install_Success_Backup"] : null;
            Package = null;
            InstallSucceeded = true;
            Installed?.Invoke(this, EventArgs.Empty);
        }
        catch (InstallException ex)
        {
            ErrorMessage = MapInstallError(ex.Code);
        }
        catch (Exception ex)
        {
            ErrorMessage = L.Format("Error_InstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void InstallAnother()
    {
        InstallSucceeded = false;
        SuccessDetail = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void OpenLanguagesFolder()
    {
        if (_gameService.Game is { } game)
            ExplorerService.OpenFolder(game.LanguagesPath);
    }

    private static string MapReadError(PackageReadError error) => error switch
    {
        PackageReadError.PathNotFound => L["Error_PathNotFound"],
        PackageReadError.NoDialogueFileFound => L["Error_NoDialogueFound"],
        PackageReadError.UnsupportedArchiveType => L["Error_UnsupportedArchive"],
        PackageReadError.ArchiveReadFailed => L["Error_ArchiveReadFailed"],
        _ => L["Error_UnsupportedFileType"],
    };

    private static string MapInstallError(InstallErrorCode code) => code switch
    {
        InstallErrorCode.GameFolderMissing => L["Error_GameFolderMissing"],
        InstallErrorCode.ReservedName => L["Error_ReservedName"],
        InstallErrorCode.ArchiveEntryOutsideTarget => L["Error_ZipSlip"],
        _ => L.Format("Error_InstallFailed", code),
    };

    partial void OnCommunityModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ManualMode));
        OnPropertyChanged(string.Empty);
        if (value && !_catalogLoaded && !IsCatalogLoading)
            _ = LoadCatalogAsync();
    }

    partial void OnIsCatalogLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowCommunityEmpty));

    partial void OnCatalogErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCatalogError));
        OnPropertyChanged(nameof(ShowCommunityEmpty));
    }

    partial void OnPackageChanged(TranslationPackage? value) => OnPropertyChanged(string.Empty);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(string.Empty);

    partial void OnInstallSucceededChanged(bool value) => OnPropertyChanged(string.Empty);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnGameErrorChanged(string? value) => OnPropertyChanged(nameof(HasGameError));

    partial void OnSuccessDetailChanged(string? value) => OnPropertyChanged(nameof(HasSuccessDetail));
}
