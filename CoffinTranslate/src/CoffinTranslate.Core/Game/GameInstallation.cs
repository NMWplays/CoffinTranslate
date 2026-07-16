namespace CoffinTranslate.Core.Game;

/// <summary>How a game installation was discovered.</summary>
public enum GameSource
{
    Steam,
    Manual,
}

/// <summary>A located installation of The Coffin of Andy and Leyley.</summary>
public sealed record GameInstallation(string RootPath, GameSource Source)
{
    public string WwwPath => Path.Combine(RootPath, "www");

    /// <summary>The game executable (Windows). Used to launch the game from the app.</summary>
    public string GameExePath => Path.Combine(RootPath, "Game.exe");

    /// <summary>Folder the game scans for translation packages. May not exist yet on older installs.</summary>
    public string LanguagesPath => Path.Combine(WwwPath, "languages");

    /// <summary>Folder of the official developer tool; must never be touched by installs.</summary>
    public string OfficialToolPath => Path.Combine(LanguagesPath, "tool");

    /// <summary>The source-data bundle shipped with the game, used by the editor as translation reference.</summary>
    public string TranslatorDataPath => Path.Combine(OfficialToolPath, "Translator.dat");

    /// <summary>Whether the source-data bundle is present (needed to start a new translation).</summary>
    public bool HasTranslatorData => File.Exists(TranslatorDataPath);
}
