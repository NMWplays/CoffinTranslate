namespace CoffinTranslate.Core;

/// <summary>Format of the translation data a package carries.</summary>
public enum DialogueFormat
{
    /// <summary>Plain-text project (dialogue.txt).</summary>
    Txt,

    /// <summary>Spreadsheet project (dialogue.csv).</summary>
    Csv,

    /// <summary>Compiled "Coffin Language Data" file (*.cld). Opaque, cannot be inspected.</summary>
    Cld,
}
