namespace CoffinTranslate.Core.Parsing;

/// <summary>
/// The self-describing header of a translation project (language name, font, credits),
/// as read from the top of dialogue.txt / dialogue.csv.
/// </summary>
public sealed record TranslationMetadata
{
    public string? LanguageName { get; init; }

    public string? FontFile { get; init; }

    public int? FontSize { get; init; }

    public IReadOnlyList<string> Credits { get; init; } = [];

    public static TranslationMetadata Empty { get; } = new();
}
