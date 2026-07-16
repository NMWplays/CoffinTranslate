using CoffinTranslate.Core.Parsing;

namespace CoffinTranslate.Core.Install;

public enum InstalledKind
{
    Folder,
    CldFile,
}

/// <summary>A translation found in the game's www/languages folder.</summary>
public sealed record InstalledTranslation(
    string FullPath,
    string Name,
    InstalledKind Kind,
    TranslationMetadata? Metadata,
    DialogueFormat? Format)
{
    public string DisplayLanguage =>
        !string.IsNullOrWhiteSpace(Metadata?.LanguageName) ? Metadata.LanguageName
        : Kind == InstalledKind.CldFile ? Path.GetFileNameWithoutExtension(Name)
        : Name;

    /// <summary>False for folders in www/languages that contain no dialogue file at all.</summary>
    public bool IsRecognized => Kind == InstalledKind.CldFile || Format is not null;
}
