namespace CoffinTranslate.Core.Parsing;

/// <summary>
/// Extracts the metadata header (language, font, credits) from dialogue files.
/// Formats are documented in docs/official-tool-analysis.md.
/// </summary>
public static class DialogueMetadataParser
{
    private const int MaxTxtHeaderLines = 400;

    public static TranslationMetadata Parse(string content, DialogueFormat format) => format switch
    {
        DialogueFormat.Txt => ParseTxt(content),
        DialogueFormat.Csv => ParseCsv(content),
        _ => TranslationMetadata.Empty,
    };

    /// <summary>
    /// TXT layout: [LANGUAGE] name, [FONT] File/Size pairs, [CREDITS] numbered lines.
    /// Metadata sections always precede the content sections ([LABELS], [MENUS], …).
    /// </summary>
    public static TranslationMetadata ParseTxt(string content)
    {
        string? language = null;
        string? fontFile = null;
        int? fontSize = null;
        var credits = new List<string>();

        string section = "";
        int lineCount = 0;
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } rawLine && lineCount++ < MaxTxtHeaderLines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim().ToUpperInvariant();
                if (section == "VERSION")
                    continue; // official files open with [VERSION]; skip it, the metadata follows
                if (section is not ("LANGUAGE" or "FONT" or "CREDITS"))
                    break; // reached the content sections; the header is done
                continue;
            }

            switch (section)
            {
                case "LANGUAGE":
                    language ??= line;
                    break;
                case "FONT":
                {
                    var (key, value) = SplitOnColon(line);
                    if (key.Equals("File", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                        fontFile = value;
                    else if (key.Equals("Size", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var size))
                        fontSize = size;
                    break;
                }
                case "CREDITS":
                {
                    var (_, value) = SplitOnColon(line);
                    if (value.Length > 0)
                        credits.Add(value);
                    break;
                }
            }
        }

        return new TranslationMetadata
        {
            LanguageName = language,
            FontFile = fontFile,
            FontSize = fontSize,
            Credits = credits,
        };
    }

    /// <summary>
    /// CSV layout: a "Language, Font File, Font Size" header row followed by its value row,
    /// then a "Credit 1..3" header row followed by its value row, then the content blocks.
    /// </summary>
    public static TranslationMetadata ParseCsv(string content)
    {
        var rows = CsvReader.Parse(content);

        string? language = null;
        string? fontFile = null;
        int? fontSize = null;
        var credits = new List<string>();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length == 0)
                continue;

            var head = row[0].Trim();
            if (head.Equals("Language", StringComparison.OrdinalIgnoreCase) && i + 1 < rows.Count)
            {
                var values = rows[i + 1];
                language = CellOrNull(values, 0);
                fontFile = CellOrNull(values, 1);
                if (int.TryParse(CellOrNull(values, 2), out var size))
                    fontSize = size;
            }
            else if (head.Equals("Credit 1", StringComparison.OrdinalIgnoreCase) && i + 1 < rows.Count)
            {
                credits.AddRange(rows[i + 1].Take(3).Select(c => c.Trim()).Where(c => c.Length > 0));
            }
            else if (head.Equals("Labels", StringComparison.OrdinalIgnoreCase))
            {
                break; // reached the content blocks; the header is done
            }
        }

        return new TranslationMetadata
        {
            LanguageName = language,
            FontFile = fontFile,
            FontSize = fontSize,
            Credits = credits,
        };
    }

    private static (string Key, string Value) SplitOnColon(string line)
    {
        int index = line.IndexOf(':');
        return index < 0
            ? (line.Trim(), "")
            : (line[..index].Trim(), line[(index + 1)..].Trim());
    }

    private static string? CellOrNull(string[] row, int index)
    {
        if (index >= row.Length)
            return null;

        var value = row[index].Trim();
        return value.Length > 0 ? value : null;
    }
}
