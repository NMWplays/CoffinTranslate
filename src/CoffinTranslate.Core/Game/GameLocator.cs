using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CoffinTranslate.Core.Game;

/// <summary>Finds installations of The Coffin of Andy and Leyley on this machine.</summary>
public static class GameLocator
{
    public const string GameFolderName = "The Coffin of Andy and Leyley";

    /// <summary>Searches all Steam libraries (Windows registry / Linux default paths) for the game.</summary>
    public static GameInstallation? FindSteamInstallation()
    {
        foreach (var library in EnumerateSteamLibraries())
        {
            var candidate = Path.Combine(library, "steamapps", "common", GameFolderName);
            if (LooksLikeGameFolder(candidate))
                return new GameInstallation(Path.GetFullPath(candidate), GameSource.Steam);
        }

        return null;
    }

    /// <summary>Heuristic check that a folder is a game installation (NW.js layout with a www folder).</summary>
    public static bool LooksLikeGameFolder(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return false;

        var www = Path.Combine(rootPath, "www");
        return Directory.Exists(www)
               && (File.Exists(Path.Combine(rootPath, "Game.exe")) || File.Exists(Path.Combine(www, "index.html")));
    }

    /// <summary>
    /// Validates a user-picked folder. Accepts the game root or the www folder itself
    /// (normalized back to the root). Returns null if the folder is not a game installation.
    /// </summary>
    public static GameInstallation? TryCreateManual(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }

        if (LooksLikeGameFolder(fullPath))
            return new GameInstallation(fullPath, GameSource.Manual);

        if (Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Equals("www", StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { } parent
            && LooksLikeGameFolder(parent))
        {
            return new GameInstallation(parent, GameSource.Manual);
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamLibraries()
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);

        foreach (var root in EnumerateSteamRoots())
        {
            if (!Directory.Exists(root))
                continue;

            if (seen.Add(root))
                yield return root;

            var vdfPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
                continue;

            string vdfContent;
            try
            {
                vdfContent = File.ReadAllText(vdfPath);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var library in SteamVdf.ParseLibraryPaths(vdfContent))
            {
                if (seen.Add(library))
                    yield return library;
            }
        }
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var root in WindowsSteamRoots())
                yield return root;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".steam", "root");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
            yield return Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam");
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> WindowsSteamRoots()
    {
        if (ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath") is { } userPath)
            yield return userPath.Replace('/', '\\');

        if (ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") is { } machinePath)
            yield return machinePath;

        yield return @"C:\Program Files (x86)\Steam";
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryString(RegistryKey hive, string subKey, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
