namespace CoffinTranslate.Core.Project;

/// <summary>
/// An editable translation, mirroring the structure of a <c>dialogue.txt</c> project but keeping
/// the source (English) reference alongside every editable target — the thing the official tool's
/// TXT format throws away. This is the model the editor binds to and both the reader and writer
/// serialize.
/// </summary>
public sealed class TranslationProject
{
    /// <summary>Language name as it appears in the game's Language menu.</summary>
    public string LanguageName { get; set; } = "";

    public string FontFace { get; set; } = "GameFont";

    public int FontSize { get; set; } = 28;

    /// <summary>
    /// Absolute path to a custom font file to bundle in the export's <c>font/</c> folder, or
    /// <see langword="null"/> to use a built-in font named by <see cref="FontFace"/>.
    /// </summary>
    public string? FontFilePath { get; set; }

    /// <summary>The (up to three) credit lines shown in the language picker.</summary>
    public List<string> Credits { get; set; } = ["", "", ""];

    /// <summary>
    /// Version+hash of the source data this project was built against (e.g.
    /// <c>"3.0.13 : a671af9…"</c>). Lets the editor flag a project that predates a game patch.
    /// </summary>
    public string? SourceVersionHash { get; set; }

    /// <summary>
    /// Accumulated active editing time in seconds. The editor grows this while the translator is
    /// actively typing; the statistics view shows it and derives an estimated time remaining.
    /// </summary>
    public long EditingSeconds { get; set; }

    /// <summary>
    /// Replaced game images: source key (e.g. <c>img/pictures/&lt;hash&gt;</c>) → absolute path of the
    /// user's replacement PNG. Only images the user has chosen to replace appear here.
    /// </summary>
    public Dictionary<string, string> ImageReplacements { get; } = new();

    public List<ScalarUnit> Labels { get; } = [];       // [LABELS]

    public List<ScalarUnit> Menus { get; } = [];        // [MENUS]

    public List<ScalarUnit> Speakers { get; } = [];     // [SPEAKERS]

    public List<ScalarUnit> Items { get; } = [];        // [ITEMS]

    public List<TextUnit> Descriptions { get; } = [];   // [DESCRIPTIONS]

    public List<DialogueFile> Dialogue { get; } = [];   // [CommonEvents.json], [MapXXX.json], …

    /// <summary>Every translatable unit across all sections, for progress/consistency checks.</summary>
    public IEnumerable<ITranslationUnit> AllUnits()
    {
        foreach (var u in Labels) yield return u;
        foreach (var u in Menus) yield return u;
        foreach (var u in Speakers) yield return u;
        foreach (var u in Items) yield return u;
        foreach (var u in Descriptions) yield return u;
        foreach (var file in Dialogue)
            foreach (var u in file.Entries) yield return u;
    }
}

/// <summary>Common surface of every translatable unit: has a source and an editable target.</summary>
public interface ITranslationUnit
{
    string SourceText { get; }

    string TargetText { get; }

    bool IsTranslated { get; }
}

/// <summary>A single-line unit keyed by a label/menu key or an 8-char ID.</summary>
public sealed class ScalarUnit : ITranslationUnit
{
    public required string Key { get; init; }

    public string Source { get; set; } = "";

    public string Target { get; set; } = "";

    /// <summary>
    /// Flagged by an "update from game" as new or source-changed since the last version, so the
    /// translator can find lines that need (re)doing. Cleared once the unit is edited.
    /// </summary>
    public bool IsNew { get; set; }

    string ITranslationUnit.SourceText => Source;

    string ITranslationUnit.TargetText => Target;

    public bool IsTranslated => Target.Length > 0;
}

/// <summary>A multi-line unit (item description or dialogue line group) keyed by ID.</summary>
public sealed class TextUnit : ITranslationUnit
{
    public required string Id { get; init; }

    /// <summary>Human-readable context shown in the TXT header parentheses (speaker or item name).</summary>
    public string Annotation { get; set; } = "";

    /// <summary>
    /// True when this unit is a "Show Choices" menu option: serialized under a <c>[CHOICES]</c>
    /// header as a single-line <c>#id : text</c> scalar instead of a normal <c>#id (name)</c> block.
    /// </summary>
    public bool IsChoice { get; set; }

    /// <summary>
    /// For a choice option, marks the first option of its <c>[CHOICES]</c> block — each block is one
    /// on-screen menu. The writer opens a fresh <c>[CHOICES]</c> header here; later options in the
    /// same menu continue under it.
    /// </summary>
    public bool ChoiceGroupStart { get; set; }

    public IReadOnlyList<string> Source { get; set; } = [];

    public List<string> Target { get; set; } = [];

    /// <summary>
    /// Flagged by an "update from game" as new or source-changed since the last version, so the
    /// translator can find lines that need (re)doing. Cleared once the unit is edited.
    /// </summary>
    public bool IsNew { get; set; }

    string ITranslationUnit.SourceText => string.Join("\n", Source);

    string ITranslationUnit.TargetText => string.Join("\n", Target);

    public bool IsTranslated => Target.Any(line => line.Length > 0);
}

/// <summary>One dialogue source file (e.g. <c>CommonEvents.json</c>) and its ordered entries.</summary>
public sealed class DialogueFile
{
    public required string FileName { get; init; }

    public List<TextUnit> Entries { get; } = [];
}
