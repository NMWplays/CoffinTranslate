namespace CoffinTranslate.Core.SourceData;

/// <summary>
/// The complete translatable source of the game, reconstructed from <c>Translator.dat</c>.
/// This is the reference every translation is written against: every string keyed by its
/// stable 8-character ID, plus the dialogue layout that fixes section grouping and order.
/// </summary>
public sealed record GameSourceCatalog
{
    /// <summary>Raw version+hash string, e.g. <c>"3.0.13 : a671af9ea6c1a302da7f"</c>.</summary>
    public required string VersionHash { get; init; }

    /// <summary>Game version parsed from <see cref="VersionHash"/> (e.g. <c>"3.0.13"</c>), if present.</summary>
    public string? Version { get; init; }

    /// <summary>Data hash parsed from <see cref="VersionHash"/>; changes when the game's text changes.</summary>
    public string? DataHash { get; init; }

    /// <summary>Source language name (the shipped data is <c>"English"</c>).</summary>
    public required string LanguageName { get; init; }

    /// <summary>The (up to three) credit lines of the source data.</summary>
    public IReadOnlyList<string> Credits { get; init; } = [];

    public string? FontFace { get; init; }

    public int? FontSize { get; init; }

    /// <summary>True when the source data embeds a custom font file.</summary>
    public bool HasEmbeddedFont { get; init; }

    /// <summary>System labels: key → text (the <c>[LABELS]</c> section).</summary>
    public IReadOnlyDictionary<string, string> SystemLabels { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Menu entries: source text → source text (the <c>[MENUS]</c> section).</summary>
    public IReadOnlyDictionary<string, string> Menus { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Speakers: ID → name (the <c>[SPEAKERS]</c> section).</summary>
    public IReadOnlyDictionary<string, string> Speakers { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Items: ID → name (the <c>[ITEMS]</c> section).</summary>
    public IReadOnlyDictionary<string, string> Items { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// The universal text store: ID → lines. One list element per displayed line. Holds both
    /// dialogue text and item descriptions (a description shares its item's ID).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Texts { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Dialogue layout, in order: one section per source file (CommonEvents/MapXXX).</summary>
    public IReadOnlyList<DialogueSection> Sections { get; init; } = [];

    /// <summary>Paths of translatable images embedded in the source data (e.g. <c>img/pictures/…</c>).</summary>
    public IReadOnlyList<string> ImagePaths { get; init; } = [];

    /// <summary>Total number of text entries (a rough size indicator for the whole script).</summary>
    public int TextCount => Texts.Count;
}

/// <summary>One source file's worth of dialogue, as an ordered list of entries.</summary>
public sealed record DialogueSection(string FileName, IReadOnlyList<DialogueEntry> Entries);

/// <summary>
/// A single dialogue entry: an (optional) speaker and the text IDs spoken, in order.
/// <paramref name="SpeakerId"/> is empty for narration; otherwise it keys into
/// <see cref="GameSourceCatalog.Speakers"/>. Each id in <paramref name="TextIds"/> keys into
/// <see cref="GameSourceCatalog.Texts"/>.
/// </summary>
public sealed record DialogueEntry(string SpeakerId, IReadOnlyList<string> TextIds);
