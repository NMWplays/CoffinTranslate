using System.Diagnostics;
using CoffinTranslate.Core.Game;
using CoffinTranslate.Core.SourceData;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoffinTranslate.Services;

/// <summary>
/// Holds the currently selected game installation, shared by all pages.
/// Detection order: saved path from settings, then Steam auto-detection.
/// </summary>
public sealed partial class GameService(SettingsService settings) : ObservableObject
{
    private string? _currentVersionHash;
    private bool _versionRead;

    [ObservableProperty]
    public partial GameInstallation? Game { get; set; }

    partial void OnGameChanged(GameInstallation? value)
    {
        _versionRead = false;
        _currentVersionHash = null;
    }

    /// <summary>
    /// The game's current data version+hash (from <c>Translator.dat</c>), read once and cached — lets
    /// the editor warn when a project was built against a different game version. Null if unavailable.
    /// </summary>
    public async Task<string?> GetCurrentVersionHashAsync()
    {
        if (_versionRead)
            return _currentVersionHash;

        if (Game is { HasTranslatorData: true } game)
        {
            var path = game.TranslatorDataPath;
            try
            {
                _currentVersionHash = await Task.Run(() => GameSourceReader.ReadFile(path).VersionHash);
            }
            catch
            {
                _currentVersionHash = null; // reading the source is a nicety; never block the editor
            }
        }

        _versionRead = true;
        return _currentVersionHash;
    }

    public async Task DetectAsync()
    {
        if (GameLocator.TryCreateManual(settings.Current.GamePath) is { } saved)
        {
            Game = saved;
            return;
        }

        Game = await Task.Run(GameLocator.FindSteamInstallation);
    }

    /// <summary>Whether the game can be launched (a Game.exe is present).</summary>
    public bool CanLaunch => Game is { } game && File.Exists(game.GameExePath);

    /// <summary>Launches the game executable. Best effort; no-op if it isn't present.</summary>
    public bool LaunchGame()
    {
        if (Game is not { } game || !File.Exists(game.GameExePath))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = game.GameExePath,
                WorkingDirectory = game.RootPath,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Applies a user-picked folder. Returns false if it is not a valid game folder.</summary>
    public bool TrySetManualPath(string path)
    {
        if (GameLocator.TryCreateManual(path) is not { } installation)
            return false;

        Game = installation;
        settings.Current.GamePath = installation.RootPath;
        settings.Save();
        return true;
    }
}
