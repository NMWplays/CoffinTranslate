using System.Text;

namespace CoffinTranslate.Core.Project;

/// <summary>
/// Serializes a <see cref="TranslationProject"/> to the game's <c>dialogue.txt</c> format
/// (UTF-8, no BOM), reproducing the official tool's layout: a <c>[VERSION]</c> header, colon-aligned
/// scalar sections, blank-line-separated dialogue blocks and <c>[CHOICES]</c> menus. Untranslated
/// units are written with their English source so the exported file is always a complete,
/// game-ready translation.
/// </summary>
public static class DialogueTxtWriter
{
    public static void WriteFile(TranslationProject project, string path, string newline = "\n") =>
        File.WriteAllText(path, Write(project, newline), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    public static string Write(TranslationProject project, string nl = "\n")
    {
        var sb = new StringBuilder();

        // Scalar sections are separated by one blank line. Text sections (DESCRIPTIONS + each
        // dialogue file) additionally carry one trailing blank, so a text section is followed by two
        // blank lines — matching the official tool's layout exactly.
        void Scalar(string block)
        {
            if (sb.Length > 0) sb.Append(nl).Append(nl);
            sb.Append(block);
        }

        void Text(string block)
        {
            if (sb.Length > 0) sb.Append(nl).Append(nl);
            sb.Append(block).Append(nl);
        }

        if (project.SourceVersionHash is { Length: > 0 } version)
            Scalar($"[VERSION]{nl}{version}");

        Scalar($"[LANGUAGE]{nl}{project.LanguageName}");
        Scalar(FontBlock(project, nl));
        Scalar(CreditsBlock(project, nl));
        Scalar(ScalarBlock("LABELS", project.Labels, prefix: "", nl));
        Scalar(ScalarBlock("MENUS", project.Menus, prefix: "", nl));
        Scalar(ScalarBlock("SPEAKERS", project.Speakers, prefix: "#", nl));
        Scalar(ScalarBlock("ITEMS", project.Items, prefix: "#", nl));
        Text(TextBlock("DESCRIPTIONS", project.Descriptions, nl));

        foreach (var file in project.Dialogue)
            Text(DialogueBlock(file, nl));

        return sb.ToString();
    }

    private static string FontBlock(TranslationProject project, string nl)
    {
        int width = "File".Length; // "File" and "Size" are both 4 chars
        return "[FONT]" + nl
            + ScalarLine("File", project.FontFace, width) + nl
            + ScalarLine("Size", project.FontSize.ToString(), width);
    }

    private static string CreditsBlock(TranslationProject project, string nl)
    {
        var sb = new StringBuilder("[CREDITS]");
        for (int i = 0; i < 3; i++)
            sb.Append(nl).Append(ScalarLine((i + 1).ToString(), i < project.Credits.Count ? project.Credits[i] : "", width: 1));
        return sb.ToString();
    }

    private static string ScalarBlock(string header, List<ScalarUnit> units, string prefix, string nl)
    {
        var sb = new StringBuilder('[' + header + ']');
        int width = units.Count == 0 ? 0 : units.Max(u => (prefix + u.Key).Length);
        foreach (var u in units)
            sb.Append(nl).Append(ScalarLine(prefix + u.Key, Effective(u), width));
        return sb.ToString();
    }

    private static string TextBlock(string header, IReadOnlyList<TextUnit> units, string nl)
    {
        if (units.Count == 0)
            return $"[{header}]";
        var entries = units.Select(u => NormalEntry(u, nl));
        return $"[{header}]{nl}{nl}" + string.Join(nl + nl, entries);
    }

    private static string DialogueBlock(DialogueFile file, string nl)
    {
        var entries = new List<string>();
        string? choiceBlock = null;

        void FlushChoice()
        {
            if (choiceBlock is not null)
            {
                entries.Add(choiceBlock);
                choiceBlock = null;
            }
        }

        foreach (var unit in file.Entries)
        {
            if (unit.IsChoice)
            {
                var optionLine = $"#{unit.Id} : {ChoiceText(unit)}";
                if (unit.ChoiceGroupStart || choiceBlock is null)
                {
                    FlushChoice();
                    choiceBlock = "[CHOICES]" + nl + optionLine;
                }
                else
                {
                    choiceBlock += nl + optionLine;
                }
            }
            else
            {
                FlushChoice();
                entries.Add(NormalEntry(unit, nl));
            }
        }

        FlushChoice();
        if (entries.Count == 0)
            return $"[{file.FileName}]";
        return $"[{file.FileName}]{nl}{nl}" + string.Join(nl + nl, entries);
    }

    private static string NormalEntry(TextUnit unit, string nl)
    {
        var lines = unit.IsTranslated ? unit.Target : unit.Source;
        if (lines.Count == 0)
            lines = [""];
        var sb = new StringBuilder($"#{unit.Id} ({unit.Annotation})");
        foreach (var text in lines)
            sb.Append(nl).Append(": ").Append(text);
        return sb.ToString();
    }

    /// <summary>The single line a choice option writes: its translation if present, else English.</summary>
    private static string ChoiceText(TextUnit unit)
    {
        var lines = unit.IsTranslated ? unit.Target : unit.Source;
        return lines.Count > 0 ? lines[0] : "";
    }

    /// <summary>The value written to disk: the translation if present, else the English source.</summary>
    private static string Effective(ScalarUnit unit) => unit.Target.Length > 0 ? unit.Target : unit.Source;

    private static string ScalarLine(string key, string value, int width) => key.PadRight(width) + " : " + value;
}
