using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CoffinTranslate.Core.IO;
using CoffinTranslate.Core.Project;
using CoffinTranslate.Core.SourceData;
using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoffinTranslate.ViewModels;

/// <summary>Which units the editor shows: everything, only what's left, only what's done, or only
/// lines flagged new/changed by the last "update from game".</summary>
public enum EditorFilter { All, Untranslated, Translated, New }

/// <summary>
/// The Advanced translation editor: create a fresh translation from the game's source data or open
/// an existing project/dialogue.txt, edit every string with source shown, translate the game's
/// images, and export a game-ready folder. Work is saved to a native <c>.ctproj</c> file that keeps
/// untranslated lines empty, so resuming never mistakes the English fallback for a real translation.
/// </summary>
public partial class EditorViewModel : ViewModelBase
{
    private readonly GameService _gameService;
    private readonly IFilePickerService _filePicker;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly DispatcherTimer _editClock;

    /// <summary>Only accrue editing time while the translator has typed within this window.</summary>
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(90);

    private static Localizer L => Localizer.Instance;

    private TranslationProject? _project;
    private IReadOnlyList<EditorCell> _allCells = [];
    private string? _projectPath;
    private string? _currentGameVersion;
    private bool _loading;
    private bool _dirty;
    private bool _saving;

    // active-editing-time + last-edit tracking
    private DateTime _lastActivityUtc = DateTime.MinValue;
    private readonly HashSet<EditorUnitViewModel> _tracked = [];
    private EditorUnitViewModel? _lastEditedUnit;
    private EditorSectionViewModel? _lastEditedSection;
    private int _remainingSourceWords;

    public EditorViewModel(GameService gameService, IFilePickerService filePicker, SettingsService settings)
    {
        _gameService = gameService;
        _filePicker = filePicker;
        _settings = settings;
        LanguageName = "";
        FontFace = "GameFont";
        FontSize = 28;
        Credit1 = Credit2 = Credit3 = "";
        EditorFontSize = ClampZoom(settings.Current.EditorFontSize);
        RefreshRecentProjects();

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1, settings.Current.AutosaveSeconds)) };
        _autosaveTimer.Tick += (_, _) =>
        {
            if (_dirty && !_saving && _projectPath is not null && _project is not null)
                _ = SaveAsync(_projectPath, silent: true);
        };
        _autosaveTimer.Start();

        // ticks once a second; accrues editing time only while the translator is actively typing
        _editClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _editClock.Tick += (_, _) =>
        {
            if (_project is not null && HasProject && DateTime.UtcNow - _lastActivityUtc < ActiveWindow)
                _project.EditingSeconds++;
        };
        _editClock.Start();

        _gameService.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanCreate));
            OnPropertyChanged(nameof(ShowNeedGame));
            OnPropertyChanged(nameof(CanUpdateFromGame));
        };
        L.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    [ObservableProperty]
    public partial bool HasProject { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial EditorSectionViewModel? SelectedSection { get; set; }

    [ObservableProperty]
    public partial string LanguageName { get; set; }

    // --- project settings (the "first row set": font + credits) ---

    [ObservableProperty]
    public partial string FontFace { get; set; }

    [ObservableProperty]
    public partial int FontSize { get; set; }

    [ObservableProperty]
    public partial string Credit1 { get; set; }

    [ObservableProperty]
    public partial string Credit2 { get; set; }

    [ObservableProperty]
    public partial string Credit3 { get; set; }

    [ObservableProperty]
    public partial bool ShowSettings { get; set; }

    // --- view + search/filter state ---

    [ObservableProperty]
    public partial bool ShowImagesView { get; set; }

    [ObservableProperty]
    public partial bool ShowStatsView { get; set; }

    /// <summary>Text size of the source/translation fields (editor zoom).</summary>
    [ObservableProperty]
    public partial double EditorFontSize { get; set; }

    // --- version compatibility ---

    [ObservableProperty]
    public partial bool ShowVersionWarning { get; set; }

    // --- statistics (computed on demand when the stats tab opens) ---

    [ObservableProperty]
    public partial int StatSourceWords { get; set; }

    [ObservableProperty]
    public partial int StatTargetWords { get; set; }

    [ObservableProperty]
    public partial int StatSourceChars { get; set; }

    [ObservableProperty]
    public partial int StatTargetChars { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial EditorFilter Filter { get; set; }

    /// <summary>Speaker chosen in the per-section speaker filter, or <see langword="null"/> for all.</summary>
    [ObservableProperty]
    public partial string? SelectedSpeaker { get; set; }

    /// <summary>True once an "update from game" has flagged new/changed lines to review.</summary>
    [ObservableProperty]
    public partial bool HasNewUnits { get; set; }

    /// <summary>Whether the speaker-filter dropdown is open (light-dismiss popup).</summary>
    [ObservableProperty]
    public partial bool IsSpeakerOpen { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    /// <summary>Distinct speakers in the current section (empty = no speaker filter shown).</summary>
    public ObservableCollection<string> Speakers { get; } = [];

    public bool HasSpeakerFilter => Speakers.Count > 0;

    public string SpeakerFilterLabel => SelectedSpeaker is { Length: > 0 } s ? s : L["Editor_Speaker_All"];

    // --- images ---

    [ObservableProperty]
    public partial bool ImagesBusy { get; set; }

    [ObservableProperty]
    public partial string? ImagesMessage { get; set; }

    public ObservableCollection<EditorSectionViewModel> Sections { get; } = [];

    /// <summary>Sections shown in the left list — all of them, or (while searching) only those
    /// that contain a match, so a search reaches across every map instead of the current one.</summary>
    public ObservableCollection<EditorSectionViewModel> VisibleSections { get; } = [];

    public ObservableCollection<EditorUnitViewModel> VisibleUnits { get; } = [];

    public ObservableCollection<EditorImageViewModel> Images { get; } = [];

    public ObservableCollection<RecentProjectItem> RecentProjects { get; } = [];

    public bool HasRecentProjects => RecentProjects.Count > 0;

    private bool _imagesLoaded;

    public bool CanCreate => _gameService.Game is { HasTranslatorData: true };

    public bool ShowNeedGame => _gameService.Game is not { HasTranslatorData: true };

    public bool ShowStart => !HasProject && !IsBusy;

    public bool ShowTextView => !ShowImagesView && !ShowStatsView;

    public int TotalUnits => _allCells.Count;

    public int TranslatedUnits => _allCells.Count(c => c.IsTranslated());

    public int UntranslatedUnits => TotalUnits - TranslatedUnits;

    /// <summary>Overall completion as a 0–1 fraction, for the statistics progress bar.</summary>
    public double ProgressFraction => TotalUnits == 0 ? 0 : (double)TranslatedUnits / TotalUnits;

    public string ProgressPercent => L.Format("Editor_Stats_Percent", (int)Math.Round(ProgressFraction * 100));

    public string? VersionWarningText => _project?.SourceVersionHash is { Length: > 0 } v
        ? L.Format("Editor_VersionMismatch", ShortVersion(v), ShortVersion(_currentGameVersion ?? ""))
        : null;

    public int VisibleCount => VisibleUnits.Count;

    public bool HasNoVisibleUnits => HasProject && ShowTextView && VisibleUnits.Count == 0;

    public string OverallProgress => L.Format("Editor_Progress", TranslatedUnits, TotalUnits);

    public int ImageReplacementCount => _project?.ImageReplacements.Count ?? 0;

    public string ImagesTabLabel => ImageReplacementCount > 0
        ? L.Format("Editor_Tab_ImagesN", ImageReplacementCount)
        : L["Editor_Tab_Images"];

    public string? ProjectVersionInfo =>
        _project?.SourceVersionHash is { Length: > 0 } v
            ? L.Format("Editor_BasedOn", ShortVersion(v)) // just the version, not the long data hash
            : null;

    private static string ShortVersion(string versionHash) => versionHash.Split(':')[0].Trim();

    public string? ProjectFileName => _projectPath is null ? null : Path.GetFileName(_projectPath);

    public bool HasFontFile => _project?.FontFilePath is { Length: > 0 };

    public string? FontFileName => _project?.FontFilePath is { } p ? Path.GetFileName(p) : null;

    /// <summary>Font used by the settings preview. When a real font file is bundled we try to load it
    /// so the sample shows that font; engine font names like "GameFont" aren't installed here, so we
    /// fall back to the default UI font. Binding the raw <see cref="FontFace"/> string to FontFamily
    /// instead throws a conversion error, which is what this property avoids.</summary>
    public FontFamily PreviewFontFamily
    {
        get
        {
            if (_project?.FontFilePath is { Length: > 0 } path && File.Exists(path))
            {
                try
                {
                    return new FontFamily(new Uri(path), "#" + Path.GetFileNameWithoutExtension(path));
                }
                catch
                {
                    // unloadable font file: fall back to the default font below
                }
            }
            return FontFamily.Default;
        }
    }

    public bool IsFilterAll => Filter == EditorFilter.All;

    public bool IsFilterUntranslated => Filter == EditorFilter.Untranslated;

    public bool IsFilterTranslated => Filter == EditorFilter.Translated;

    public bool IsFilterNew => Filter == EditorFilter.New;

    public bool HasError => ErrorMessage is not null;

    public bool HasStatus => StatusMessage is not null;

    /// <summary>Whether the row below the top islands has anything to show (project settings card,
    /// a status/error message, or the version-mismatch banner).</summary>
    public bool ShowTopExtras => ShowSettings || HasError || HasStatus || ShowVersionWarning;

    /// <summary>Whether the game folder is present so an "update from game" is possible.</summary>
    public bool CanUpdateFromGame => _gameService.Game is { HasTranslatorData: true };

    /// <summary>Raised to ask the view to scroll a specific unit into view (jump-to-last-edit).</summary>
    public event Action<EditorUnitViewModel>? ScrollToUnitRequested;

    public bool CanJumpToLastEdit => _lastEditedUnit is not null;

    // --- statistics: editing time + estimate ---

    public string EditingTimeText => FormatDuration(_project?.EditingSeconds ?? 0);

    /// <summary>Time-left estimate from the achieved words-per-second rate, or "—" without enough data.</summary>
    public string EstimatedRemainingText
    {
        get
        {
            if (UntranslatedUnits == 0)
                return FormatDuration(0);
            var seconds = _project?.EditingSeconds ?? 0;
            if (seconds < 60 || StatTargetWords < 20 || _remainingSourceWords == 0)
                return L["Editor_Stats_Remaining_Unknown"];
            var wordsPerSecond = (double)StatTargetWords / seconds;
            return FormatDuration((long)Math.Round(_remainingSourceWords / wordsPerSecond));
        }
    }

    private static string FormatDuration(long seconds)
    {
        if (seconds < 60)
            return $"{seconds} s";
        var minutes = seconds / 60;
        if (minutes < 60)
            return $"{minutes} min";
        var hours = minutes / 60;
        if (hours < 48)
            return $"{hours} h {minutes % 60} min";
        return $"{hours / 24} d {hours % 24} h";
    }

    // --- create / open ---

    [RelayCommand]
    private async Task CreateFromGameAsync()
    {
        if (_gameService.Game is not { HasTranslatorData: true } game)
            return;

        await LoadAsync(() =>
        {
            var project = TranslationProjectFactory.FromCatalog(GameSourceReader.ReadFile(game.TranslatorDataPath));
            if (_settings.Current.DefaultFontFace is { Length: > 0 } font)
                project.FontFace = font;
            return project;
        });
    }

    [RelayCommand]
    private async Task OpenExistingAsync()
    {
        var fileTypes = new List<FilePickerFileType>
        {
            new(L["Editor_OpenFileType"]) { Patterns = ["*.txt", "*.csv"] },
            FilePickerFileTypes.All,
        };
        var path = await _filePicker.PickFileAsync(L["Editor_OpenTitle"], fileTypes);
        if (path is null)
            return;

        await LoadAsync(() =>
        {
            var isCsv = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);
            var project = isCsv
                ? DialogueCsvReader.Read(File.ReadAllText(path))
                : DialogueTxtReader.Read(File.ReadAllText(path));
            if (_gameService.Game is { HasTranslatorData: true } game)
            {
                try
                {
                    TranslationProjectFactory.MergeSource(project, GameSourceReader.ReadFile(game.TranslatorDataPath));
                }
                catch
                {
                    // source reference is a nicety — editing still works without it
                }
            }

            return project;
        });
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var fileTypes = new List<FilePickerFileType>
        {
            new(L["Editor_ProjectFileType"]) { Patterns = ["*.ctproj"] },
            FilePickerFileTypes.All,
        };
        var path = await _filePicker.PickFileAsync(L["Editor_OpenProjectTitle"], fileTypes);
        if (path is null)
            return;

        if (await LoadAsync(() => TranslationProjectJson.ReadFile(path)))
        {
            _projectPath = path;
            OnPropertyChanged(nameof(ProjectFileName));
            RememberProject(path);
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(RecentProjectItem? item)
    {
        if (item is null)
            return;

        if (!File.Exists(item.Path))
        {
            _settings.RemoveRecentProject(item.Path);
            RefreshRecentProjects();
            ErrorMessage = L.Format("Editor_RecentMissing", item.Name);
            return;
        }

        if (await LoadAsync(() => TranslationProjectJson.ReadFile(item.Path)))
        {
            _projectPath = item.Path;
            OnPropertyChanged(nameof(ProjectFileName));
            RememberProject(item.Path);
        }
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var path in _settings.Current.RecentProjects)
            RecentProjects.Add(new RecentProjectItem(path));
        OnPropertyChanged(nameof(HasRecentProjects));
    }

    private void RememberProject(string path)
    {
        _settings.AddRecentProject(path);
        RefreshRecentProjects();
    }

    // --- save ---

    [RelayCommand]
    private Task SaveProjectAsync() =>
        _projectPath is null ? SaveProjectAsAsync() : SaveAsync(_projectPath, silent: false);

    [RelayCommand]
    private async Task SaveProjectAsAsync()
    {
        if (_project is null)
            return;

        var suggested = NameUtils.SanitizeFileName(LanguageName) is { Length: > 0 } n ? n : "translation";
        var path = await _filePicker.SaveFileAsync(
            L["Editor_SaveTitle"],
            suggested + TranslationProjectJson.FileExtension,
            [new(L["Editor_ProjectFileType"]) { Patterns = ["*.ctproj"] }]);
        if (path is null)
            return;

        if (!path.EndsWith(TranslationProjectJson.FileExtension, StringComparison.OrdinalIgnoreCase))
            path += TranslationProjectJson.FileExtension;

        _projectPath = path;
        OnPropertyChanged(nameof(ProjectFileName));
        await SaveAsync(path, silent: false);
    }

    private async Task SaveAsync(string path, bool silent)
    {
        if (_project is null || _saving)
            return;

        _saving = true;
        try
        {
            var project = _project;
            await Task.Run(() => TranslationProjectJson.WriteFile(project, path));
            _dirty = false;
            HasUnsavedChanges = false;
            if (!silent)
            {
                ErrorMessage = null;
                StatusMessage = L.Format("Editor_Saved", Path.GetFileName(path));
                RememberProject(path);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = L.Format("Editor_SaveFailed", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    // --- export (game-ready folder: dialogue.txt + images + font) ---

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_project is null)
            return;

        if (string.IsNullOrWhiteSpace(LanguageName))
        {
            ErrorMessage = L["Editor_NeedLanguageName"];
            return;
        }

        var folder = await _filePicker.PickFolderAsync(L["Editor_ExportTitle"]);
        if (folder is null)
            return;

        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var name = NameUtils.SanitizeFileName(LanguageName);
            if (name.Length == 0)
                name = "translation";

            var dir = Path.Combine(folder, name);
            var project = _project;
            var missing = await Task.Run(() =>
            {
                Directory.CreateDirectory(dir);
                DialogueTxtWriter.WriteFile(project, Path.Combine(dir, "dialogue.txt"));
                var missingImages = TranslationImageExporter.Export(project, dir);
                CopyFontFile(project, dir);
                return missingImages.Count;
            });

            StatusMessage = missing > 0
                ? L.Format("Editor_ExportDoneMissing", dir, missing)
                : L.Format("Editor_ExportDone", dir);
        }
        catch (Exception ex)
        {
            ErrorMessage = L.Format("Editor_ExportFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportZipAsync()
    {
        if (_project is null)
            return;

        if (string.IsNullOrWhiteSpace(LanguageName))
        {
            ErrorMessage = L["Editor_NeedLanguageName"];
            return;
        }

        var name = NameUtils.SanitizeFileName(LanguageName);
        if (name.Length == 0)
            name = "translation";

        var zipPath = await _filePicker.SaveFileAsync(
            L["Editor_ExportZipTitle"], name + ".zip",
            [new(L["Editor_ZipFileType"]) { Patterns = ["*.zip"] }]);
        if (zipPath is null)
            return;

        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var project = _project;
            var missing = await Task.Run(() =>
            {
                var temp = Path.Combine(Path.GetTempPath(), "CoffinTranslate", "zip_" + Guid.NewGuid().ToString("N"));
                var dir = Path.Combine(temp, name);
                Directory.CreateDirectory(dir);
                DialogueTxtWriter.WriteFile(project, Path.Combine(dir, "dialogue.txt"));
                var missingImages = TranslationImageExporter.Export(project, dir);
                CopyFontFile(project, dir);

                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                ZipFile.CreateFromDirectory(temp, zipPath);
                try { Directory.Delete(temp, recursive: true); } catch { /* temp cleanup is best effort */ }
                return missingImages.Count;
            });

            StatusMessage = missing > 0
                ? L.Format("Editor_ExportZipDoneMissing", zipPath, missing)
                : L.Format("Editor_ExportZipDone", zipPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = L.Format("Editor_ExportFailed", ex.Message);
        }
    }

    private static void CopyFontFile(TranslationProject project, string dir)
    {
        if (project.FontFilePath is not { Length: > 0 } fontPath || !File.Exists(fontPath))
            return;

        var fontDir = Path.Combine(dir, "font");
        Directory.CreateDirectory(fontDir);
        File.Copy(fontPath, Path.Combine(fontDir, Path.GetFileName(fontPath)), overwrite: true);
    }

    // --- font file ---

    [RelayCommand]
    private async Task PickFontFileAsync()
    {
        if (_project is null)
            return;

        var path = await _filePicker.PickFileAsync(
            L["Editor_Font_PickTitle"],
            [
                new(L["Editor_Font_FileType"]) { Patterns = ["*.ttf", "*.otf", "*.woff", "*.woff2", "*.eot", "*.svg"] },
                FilePickerFileTypes.All,
            ]);
        if (path is null)
            return;

        _project.FontFilePath = path;
        FontFace = Path.GetFileName(path); // [FONT] File must name the bundled font file
        OnPropertyChanged(nameof(HasFontFile));
        OnPropertyChanged(nameof(FontFileName));
        OnPropertyChanged(nameof(PreviewFontFamily));
        MarkDirty();
    }

    [RelayCommand]
    private void ClearFontFile()
    {
        if (_project is null)
            return;

        _project.FontFilePath = null;
        OnPropertyChanged(nameof(HasFontFile));
        OnPropertyChanged(nameof(FontFileName));
        OnPropertyChanged(nameof(PreviewFontFamily));
        MarkDirty();
    }

    // --- views + filter ---

    [RelayCommand]
    private void ToggleSettings() => ShowSettings = !ShowSettings;

    [RelayCommand]
    private async Task ShowImages()
    {
        ShowStatsView = false;
        ShowImagesView = true;
        await LoadImagesAsync();
    }

    [RelayCommand]
    private void ShowTexts()
    {
        ShowImagesView = false;
        ShowStatsView = false;
    }

    [RelayCommand]
    private void ShowStats()
    {
        ShowImagesView = false;
        ShowStatsView = true;
        ComputeStats();
    }

    [RelayCommand]
    private void SetFilter(EditorFilter filter) => Filter = filter;

    [RelayCommand]
    private void ToggleSpeaker() => IsSpeakerOpen = !IsSpeakerOpen;

    [RelayCommand]
    private void SetSpeaker(string? speaker)
    {
        SelectedSpeaker = string.IsNullOrEmpty(speaker) ? null : speaker;
        IsSpeakerOpen = false;
    }

    /// <summary>Selects the section of the last line the translator edited and scrolls it into view.</summary>
    [RelayCommand]
    private void JumpToLastEdit()
    {
        if (_lastEditedUnit is null || _lastEditedSection is null)
            return;

        if (!ReferenceEquals(SelectedSection, _lastEditedSection))
            SelectedSection = _lastEditedSection; // rebuilds the visible list for that section

        if (!VisibleUnits.Contains(_lastEditedUnit))
        {
            // clear any active filter/search/speaker so the target is guaranteed visible
            SelectedSpeaker = null;
            SearchText = "";
            Filter = EditorFilter.All;
        }

        ScrollToUnitRequested?.Invoke(_lastEditedUnit);
    }

    // --- update from a newer game version ---

    /// <summary>
    /// Re-reads the game's source, folds in strings that were added or whose English changed,
    /// flags them for review, and refreshes the version stamp. Existing translations are kept.
    /// </summary>
    [RelayCommand]
    private async Task UpdateFromGameAsync()
    {
        if (_project is null || _gameService.Game is not { HasTranslatorData: true } game)
            return;

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            var project = _project;
            var result = await Task.Run(() =>
                TranslationProjectFactory.UpdateFromCatalog(project, GameSourceReader.ReadFile(game.TranslatorDataPath)));

            _loading = true;
            _tracked.Clear();
            BuildSections(project);
            _currentGameVersion = project.SourceVersionHash;
            ShowVersionWarning = false;
            HasNewUnits = CountNew() > 0;
            _loading = false;

            OnPropertyChanged(nameof(ProjectVersionInfo));
            OnPropertyChanged(nameof(VersionWarningText));
            if (HasNewUnits)
                Filter = EditorFilter.New;
            RefreshProgress();
            MarkDirty();

            StatusMessage = result.HasChanges
                ? L.Format("Editor_UpdateDone", result.Added, result.Changed)
                : L["Editor_UpdateNone"];
        }
        catch (Exception ex)
        {
            ErrorMessage = L.Format("Editor_UpdateFailed", ex.Message);
        }
        finally
        {
            _loading = false;
            IsBusy = false;
        }
    }

    private int CountNew()
    {
        if (_project is null)
            return 0;
        var n = 0;
        foreach (var u in _project.Labels) if (u.IsNew) n++;
        foreach (var u in _project.Menus) if (u.IsNew) n++;
        foreach (var u in _project.Speakers) if (u.IsNew) n++;
        foreach (var u in _project.Items) if (u.IsNew) n++;
        foreach (var u in _project.Descriptions) if (u.IsNew) n++;
        foreach (var file in _project.Dialogue)
            foreach (var u in file.Entries) if (u.IsNew) n++;
        return n;
    }

    private void RecomputeSpeakers()
    {
        Speakers.Clear();
        if (SelectedSection is not null)
        {
            foreach (var name in SelectedSection.Units
                         .Select(u => u.Speaker)
                         .Where(s => s.Length > 0)
                         .Distinct()
                         .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
                Speakers.Add(name);
        }

        OnPropertyChanged(nameof(HasSpeakerFilter));
    }

    // --- editor zoom ---

    [RelayCommand]
    private void ZoomIn() => EditorFontSize = ClampZoom(EditorFontSize + 1);

    [RelayCommand]
    private void ZoomOut() => EditorFontSize = ClampZoom(EditorFontSize - 1);

    [RelayCommand]
    private void ZoomReset() => EditorFontSize = 14;

    private static double ClampZoom(double value) => value < 9 ? 9 : value > 30 ? 30 : value;

    partial void OnEditorFontSizeChanged(double value)
    {
        _settings.Current.EditorFontSize = value;
        _settings.Save();
    }

    // --- statistics ---

    private void ComputeStats()
    {
        int sw = 0, tw = 0, sc = 0, tc = 0, remaining = 0;
        foreach (var cell in _allCells)
        {
            var sourceWords = WordCount(cell.Source);
            sw += sourceWords;
            sc += cell.Source.Length;
            var target = cell.ReadTarget();
            tw += WordCount(target);
            tc += target.Length;
            if (!cell.IsTranslated())
                remaining += sourceWords;
        }

        StatSourceWords = sw;
        StatTargetWords = tw;
        StatSourceChars = sc;
        StatTargetChars = tc;
        _remainingSourceWords = remaining;

        foreach (var section in Sections)
            section.RefreshProgress();

        OnPropertyChanged(nameof(UntranslatedUnits));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(EditingTimeText));
        OnPropertyChanged(nameof(EstimatedRemainingText));
        RefreshProgress();
    }

    private static int WordCount(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    // --- version compatibility ---

    private async Task CheckVersionAsync()
    {
        ShowVersionWarning = false;
        _currentGameVersion = null;
        if (_project?.SourceVersionHash is not { Length: > 0 } projectVersion)
            return;

        var current = await _gameService.GetCurrentVersionHashAsync();
        if (current is not { Length: > 0 })
            return;

        _currentGameVersion = current;
        if (!string.Equals(ShortVersion(projectVersion), ShortVersion(current), StringComparison.OrdinalIgnoreCase))
        {
            ShowVersionWarning = true;
            OnPropertyChanged(nameof(VersionWarningText));
        }
    }

    // --- images ---

    [RelayCommand]
    private async Task ReplaceImageAsync(EditorImageViewModel? image)
    {
        if (_project is null || image is null)
            return;

        var path = await _filePicker.PickFileAsync(
            L["Editor_Images_PickTitle"],
            [
                new(L["Editor_Images_FileType"]) { Patterns = ["*.png"] },
                FilePickerFileTypes.All,
            ]);
        if (path is null)
            return;

        _project.ImageReplacements[image.Key] = path;
        image.SetReplacement(path);
        OnImageReplacementsChanged();
        MarkDirty();
    }

    [RelayCommand]
    private void ClearImage(EditorImageViewModel? image)
    {
        if (_project is null || image is null)
            return;

        _project.ImageReplacements.Remove(image.Key);
        image.SetReplacement(null);
        OnImageReplacementsChanged();
        MarkDirty();
    }

    /// <summary>Opens the original game image full-size in the OS default viewer (writes it to a temp PNG).</summary>
    [RelayCommand]
    private void OpenImage(EditorImageViewModel? image)
    {
        if (image?.OriginalBytes is not { Length: > 0 } bytes)
            return;

        try
        {
            var path = Path.Combine(
                Path.GetTempPath(), "CoffinTranslate", "img", image.Key.Replace('/', '_') + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // opening an external viewer is best effort
        }
    }

    private void OnImageReplacementsChanged()
    {
        OnPropertyChanged(nameof(ImageReplacementCount));
        OnPropertyChanged(nameof(ImagesTabLabel));
    }

    private async Task LoadImagesAsync()
    {
        if (_imagesLoaded || _project is null)
            return;

        if (_gameService.Game is not { HasTranslatorData: true } game)
        {
            ImagesMessage = L["Editor_Images_NeedGame"];
            return;
        }

        ImagesBusy = true;
        ImagesMessage = null;
        try
        {
            var replacements = new Dictionary<string, string>(_project.ImageReplacements);
            var built = await Task.Run(() =>
            {
                var list = new List<EditorImageViewModel>();
                foreach (var (key, bytes) in GameSourceReader.ReadImages(game.TranslatorDataPath)
                             .OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    Bitmap? preview = null;
                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        preview = Bitmap.DecodeToWidth(ms, 260);
                    }
                    catch
                    {
                        // a single undecodable image shouldn't sink the whole panel
                    }

                    replacements.TryGetValue(key, out var replacement);
                    list.Add(new EditorImageViewModel(key, preview, bytes, replacement));
                }

                return list;
            });

            Images.Clear();
            foreach (var vm in built)
                Images.Add(vm);
            _imagesLoaded = true;

            if (Images.Count == 0)
                ImagesMessage = L["Editor_Images_None"];
        }
        catch (Exception ex)
        {
            ImagesMessage = L.Format("Editor_Images_Failed", ex.Message);
        }
        finally
        {
            ImagesBusy = false;
        }
    }

    // --- close ---

    [RelayCommand]
    private void CloseProject()
    {
        _project = null;
        _allCells = [];
        _projectPath = null;
        _imagesLoaded = false;
        _dirty = false;
        _tracked.Clear();
        _lastEditedUnit = null;
        _lastEditedSection = null;
        _remainingSourceWords = 0;
        Sections.Clear();
        VisibleSections.Clear();
        VisibleUnits.Clear();
        Images.Clear();
        Speakers.Clear();
        SelectedSection = null;
        HasProject = false;
        HasUnsavedChanges = false;
        HasNewUnits = false;
        ShowImagesView = false;
        ShowStatsView = false;
        ShowVersionWarning = false;
        ShowSettings = false;
        SearchText = "";
        SelectedSpeaker = null;
        Filter = EditorFilter.All;
        LanguageName = "";
        StatusMessage = null;
        ErrorMessage = null;
        ImagesMessage = null;
        OnPropertyChanged(nameof(ProjectFileName));
        OnPropertyChanged(nameof(ProjectVersionInfo));
        OnPropertyChanged(nameof(CanJumpToLastEdit));
        OnPropertyChanged(nameof(HasSpeakerFilter));
        OnPropertyChanged(nameof(SpeakerFilterLabel));
        OnImageReplacementsChanged();
        OnPropertyChanged(nameof(HasFontFile));
        OnPropertyChanged(nameof(FontFileName));
        OnPropertyChanged(nameof(PreviewFontFamily));
        RefreshProgress();
    }

    // --- loading ---

    private async Task<bool> LoadAsync(Func<TranslationProject> build)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        ImagesMessage = null;
        try
        {
            var project = await Task.Run(build);

            _loading = true;
            _project = project;
            _projectPath = null;
            _imagesLoaded = false;
            _dirty = false;
            Images.Clear();
            ShowImagesView = false;
            ShowStatsView = false;
            ShowSettings = false;
            ShowVersionWarning = false;
            SearchText = "";
            Filter = EditorFilter.All;

            BuildSections(project);
            LanguageName = project.LanguageName;
            FontFace = project.FontFace;
            FontSize = project.FontSize;
            Credit1 = Credit(project, 0);
            Credit2 = Credit(project, 1);
            Credit3 = Credit(project, 2);
            HasProject = true;
            HasUnsavedChanges = false;
            HasNewUnits = CountNew() > 0;

            OnPropertyChanged(nameof(ProjectFileName));
            OnPropertyChanged(nameof(ProjectVersionInfo));
            OnPropertyChanged(nameof(VersionWarningText));
            OnPropertyChanged(nameof(HasFontFile));
            OnPropertyChanged(nameof(FontFileName));
            OnPropertyChanged(nameof(PreviewFontFamily));
            OnImageReplacementsChanged();
            _ = CheckVersionAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = L.Format("Editor_OpenFailed", ex.Message);
            return false;
        }
        finally
        {
            _loading = false;
            IsBusy = false;
            RefreshProgress();
        }
    }

    private static string Credit(TranslationProject p, int i) => i < p.Credits.Count ? p.Credits[i] : "";

    private void BuildSections(TranslationProject project)
    {
        Sections.Clear();
        _tracked.Clear();
        _lastEditedUnit = null;
        _lastEditedSection = null;
        OnPropertyChanged(nameof(CanJumpToLastEdit));
        var all = new List<EditorCell>();

        void AddScalar(string title, List<ScalarUnit> units)
        {
            var cells = units.Select(ScalarCell).ToList();
            all.AddRange(cells);
            Sections.Add(new EditorSectionViewModel(title, cells, OnProgressChanged));
        }

        void AddText(string title, IEnumerable<EditorCell> cells)
        {
            var list = cells.ToList();
            all.AddRange(list);
            Sections.Add(new EditorSectionViewModel(title, list, OnProgressChanged));
        }

        AddScalar(L["Editor_Section_Labels"], project.Labels);
        AddScalar(L["Editor_Section_Menus"], project.Menus);
        AddScalar(L["Editor_Section_Speakers"], project.Speakers);
        AddScalar(L["Editor_Section_Items"], project.Items);
        AddText(L["Editor_Section_Descriptions"],
            project.Descriptions.Select(u => TextCell(u, $"#{u.Id} ({u.Annotation})")));

        foreach (var file in project.Dialogue)
            AddText(file.FileName, file.Entries.Select(u => TextCell(u, UnitHeader(u), u.IsChoice ? "" : u.Annotation)));

        _allCells = all;
        RebuildVisibleSections(); // mirrors Sections into VisibleSections and selects the first
    }

    private static string UnitHeader(TextUnit u) => u.IsChoice
        ? $"#{u.Id} ({L["Editor_Choice"]})"
        : u.Annotation.Length > 0 ? $"#{u.Id} ({u.Annotation})" : $"#{u.Id}";

    private static EditorCell ScalarCell(ScalarUnit u) =>
        new(u.Key, u.Source, () => u.Target, v => u.Target = v, () => u.IsTranslated,
            IsNew: () => u.IsNew, ClearNew: () => u.IsNew = false);

    private static EditorCell TextCell(TextUnit u, string header, string speaker = "") =>
        new(header, string.Join("\n", u.Source),
            () => string.Join("\n", u.Target),
            v => u.Target = SplitLines(v),
            () => u.IsTranslated,
            Speaker: speaker,
            IsNew: () => u.IsNew,
            ClearNew: () => u.IsNew = false);

    private static List<string> SplitLines(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

    // --- filtering ---

    /// <summary>Filters the left section list to matches while a search is active (so searching
    /// spans every map), and keeps a valid section selected.</summary>
    private void RebuildVisibleSections()
    {
        var search = SearchText?.Trim() ?? "";
        // capture first: clearing the bound collection nulls the ListBox selection via the binding,
        // which would otherwise make us fall through to "first section" and lose the user's place
        var current = SelectedSection;

        VisibleSections.Clear();
        foreach (var section in Sections)
            if (search.Length == 0 || section.MatchesSearch(search))
                VisibleSections.Add(section);

        SelectedSection = current is not null && VisibleSections.Contains(current)
            ? current
            : VisibleSections.FirstOrDefault();
    }

    private void RebuildVisibleUnits()
    {
        VisibleUnits.Clear();
        if (SelectedSection is not null)
        {
            var search = SearchText?.Trim() ?? "";
            var speaker = SelectedSpeaker;
            foreach (var unit in SelectedSection.Units)
            {
                TrackUnit(unit);
                if (Filter == EditorFilter.Untranslated && unit.IsTranslated)
                    continue;
                if (Filter == EditorFilter.Translated && !unit.IsTranslated)
                    continue;
                if (Filter == EditorFilter.New && !unit.IsNew)
                    continue;
                if (speaker is { Length: > 0 } && !string.Equals(unit.Speaker, speaker, StringComparison.Ordinal))
                    continue;
                if (search.Length > 0 && !Matches(unit, search))
                    continue;
                VisibleUnits.Add(unit);
            }
        }

        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(HasNoVisibleUnits));
    }

    /// <summary>Subscribes once to a row so edits update the "active editing" clock and the
    /// jump-to-last-edit target.</summary>
    private void TrackUnit(EditorUnitViewModel unit)
    {
        if (_tracked.Add(unit))
            unit.PropertyChanged += OnUnitPropertyChanged;
    }

    private void OnUnitPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditorUnitViewModel.TargetText) || sender is not EditorUnitViewModel unit)
            return;

        _lastActivityUtc = DateTime.UtcNow;
        _lastEditedUnit = unit;
        _lastEditedSection = SelectedSection;
        OnPropertyChanged(nameof(CanJumpToLastEdit));
    }

    private static bool Matches(EditorUnitViewModel u, string search) =>
        u.Header.Contains(search, StringComparison.OrdinalIgnoreCase)
        || u.Source.Contains(search, StringComparison.OrdinalIgnoreCase)
        || u.TargetText.Contains(search, StringComparison.OrdinalIgnoreCase);

    // --- progress + dirty ---

    private void OnProgressChanged()
    {
        OnPropertyChanged(nameof(TranslatedUnits));
        OnPropertyChanged(nameof(OverallProgress));
        MarkDirty();
    }

    private void RefreshProgress()
    {
        OnPropertyChanged(nameof(TotalUnits));
        OnPropertyChanged(nameof(TranslatedUnits));
        OnPropertyChanged(nameof(OverallProgress));
    }

    private void MarkDirty()
    {
        if (_loading || _project is null)
            return;

        _dirty = true;
        HasUnsavedChanges = true;
    }

    // --- change reactions ---

    partial void OnHasProjectChanged(bool value) => OnPropertyChanged(nameof(ShowStart));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowStart));

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowTopExtras));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(ShowTopExtras));
    }

    partial void OnShowSettingsChanged(bool value) => OnPropertyChanged(nameof(ShowTopExtras));

    partial void OnShowVersionWarningChanged(bool value) => OnPropertyChanged(nameof(ShowTopExtras));

    partial void OnShowImagesViewChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTextView));
        OnPropertyChanged(nameof(HasNoVisibleUnits));
    }

    partial void OnShowStatsViewChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTextView));
        OnPropertyChanged(nameof(HasNoVisibleUnits));
    }

    partial void OnSelectedSectionChanged(EditorSectionViewModel? value)
    {
        if (value is not null)
        {
            ShowImagesView = false;
            ShowStatsView = false;
        }
        IsSpeakerOpen = false;
        RecomputeSpeakers();
        if (SelectedSpeaker is not null)
            SelectedSpeaker = null; // triggers a rebuild via OnSelectedSpeakerChanged
        else
            RebuildVisibleUnits();
    }

    partial void OnSelectedSpeakerChanged(string? value)
    {
        OnPropertyChanged(nameof(SpeakerFilterLabel));
        RebuildVisibleUnits();
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildVisibleSections(); // may reselect a section (→ rebuilds units)…
        RebuildVisibleUnits();    // …but also refresh units when the section stayed the same
    }

    partial void OnFilterChanged(EditorFilter value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterUntranslated));
        OnPropertyChanged(nameof(IsFilterTranslated));
        OnPropertyChanged(nameof(IsFilterNew));
        RebuildVisibleUnits();
    }

    partial void OnLanguageNameChanged(string value)
    {
        if (_project is not null)
        {
            _project.LanguageName = value;
            MarkDirty();
        }
    }

    partial void OnFontFaceChanged(string value)
    {
        if (_project is not null)
        {
            _project.FontFace = value;
            MarkDirty();
        }
    }

    /// <summary>Font size used by the settings preview — follows the project size but capped so an
    /// extreme game size can't blow up the panel.</summary>
    public double PreviewFontSize => Math.Clamp((double)FontSize, 10, 56);

    partial void OnFontSizeChanged(int value)
    {
        OnPropertyChanged(nameof(PreviewFontSize));
        if (_project is not null)
        {
            _project.FontSize = value;
            MarkDirty();
        }
    }

    partial void OnCredit1Changed(string value) => SetCredit(0, value);

    partial void OnCredit2Changed(string value) => SetCredit(1, value);

    partial void OnCredit3Changed(string value) => SetCredit(2, value);

    private void SetCredit(int index, string value)
    {
        if (_project is null)
            return;

        while (_project.Credits.Count <= index)
            _project.Credits.Add("");
        _project.Credits[index] = value;
        MarkDirty();
    }
}
