using System.Text.Json;

namespace CoffinTranslate.Services;

public sealed class AppSettings
{
    public string? UiLanguage { get; set; }

    public string? GamePath { get; set; }

    /// <summary>Theme override: <c>"light"</c>, <c>"dark"</c>, or <c>null</c>/other = follow the system.</summary>
    public string? Theme { get; set; }

    /// <summary>Recently opened/saved editor project files, most-recent first.</summary>
    public List<string> RecentProjects { get; set; } = [];

    /// <summary>Editor text size (zoom) for the source/translation fields.</summary>
    public double EditorFontSize { get; set; } = 14;

    /// <summary>Autosave interval in seconds for the editor.</summary>
    public int AutosaveSeconds { get; set; } = 4;

    /// <summary>Default font face pre-filled into a newly created translation project (null = GameFont).</summary>
    public string? DefaultFontFace { get; set; }
}

/// <summary>Persists app settings as JSON in the local application data folder.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoffinTranslate",
            "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (IOException)
        {
            // settings are a convenience — never crash the app over them
        }
    }

    private const int MaxRecentProjects = 8;

    /// <summary>Records a project path at the top of the recent list (deduplicated) and persists.</summary>
    public void AddRecentProject(string path)
    {
        var list = Current.RecentProjects;
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxRecentProjects)
            list.RemoveRange(MaxRecentProjects, list.Count - MaxRecentProjects);
        Save();
    }

    /// <summary>Removes a project path from the recent list (e.g. after it fails to open) and persists.</summary>
    public void RemoveRecentProject(string path)
    {
        if (Current.RecentProjects.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0)
            Save();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }

        return new AppSettings();
    }
}
