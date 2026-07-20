namespace CoffinTranslate.Core.Project;

/// <summary>
/// Parses an existing <c>dialogue.txt</c> into a <see cref="TranslationProject"/>. Because the TXT
/// format stores only the translation, the resulting units carry their read value as
/// <c>Target</c> and an empty <c>Source</c>; the editor fills sources by matching IDs against the
/// game's source catalog.
/// </summary>
public static class DialogueTxtReader
{
    private enum Mode { None, Version, Language, Font, Credits, Labels, Menus, Speakers, Items, Descriptions, Dialogue }

    public static TranslationProject Read(string text)
    {
        var project = new TranslationProject { FontFace = "", FontSize = 0, Credits = ["", "", ""] };
        var mode = Mode.None;
        DialogueFile? currentFile = null;
        TextUnit? currentUnit = null;
        List<TextUnit>? textTarget = null;
        bool inChoices = false;
        bool choiceGroupStart = false;

        void FlushUnit()
        {
            if (currentUnit is not null)
            {
                textTarget?.Add(currentUnit);
                currentUnit = null;
            }
        }

        foreach (var raw in SplitLines(text))
        {
            var line = raw;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                FlushUnit();
                var header = line[1..^1];
                switch (header)
                {
                    case "VERSION": mode = Mode.Version; inChoices = false; break;
                    case "LANGUAGE": mode = Mode.Language; inChoices = false; break;
                    case "FONT": mode = Mode.Font; inChoices = false; break;
                    case "CREDITS": mode = Mode.Credits; inChoices = false; break;
                    case "LABELS": mode = Mode.Labels; inChoices = false; break;
                    case "MENUS": mode = Mode.Menus; inChoices = false; break;
                    case "SPEAKERS": mode = Mode.Speakers; inChoices = false; break;
                    case "ITEMS": mode = Mode.Items; inChoices = false; break;
                    case "DESCRIPTIONS": mode = Mode.Descriptions; textTarget = project.Descriptions; inChoices = false; break;
                    case "CHOICES":
                        // choices belong to the current dialogue file — do NOT start a new section
                        if (currentFile is not null)
                        {
                            mode = Mode.Dialogue;
                            inChoices = true;
                            choiceGroupStart = true;
                        }
                        break;
                    default:
                        mode = Mode.Dialogue;
                        inChoices = false;
                        currentFile = new DialogueFile { FileName = header };
                        project.Dialogue.Add(currentFile);
                        textTarget = currentFile.Entries;
                        break;
                }
                continue;
            }

            switch (mode)
            {
                case Mode.Version:
                    if (line.Length > 0 && string.IsNullOrEmpty(project.SourceVersionHash))
                        project.SourceVersionHash = line.Trim();
                    break;

                case Mode.Language:
                    if (line.Length > 0 && project.LanguageName.Length == 0)
                        project.LanguageName = line;
                    break;

                case Mode.Font:
                    if (TrySplitScalar(line, out var fk, out var fv))
                    {
                        if (fk == "File") project.FontFace = fv;
                        else if (fk == "Size" && int.TryParse(fv, out var size)) project.FontSize = size;
                    }
                    break;

                case Mode.Credits:
                    if (TrySplitScalar(line, out var ck, out var cv) && int.TryParse(ck, out var idx) && idx is >= 1 and <= 3)
                        project.Credits[idx - 1] = cv;
                    break;

                case Mode.Labels: AddScalar(project.Labels, line, stripHash: false); break;
                case Mode.Menus: AddScalar(project.Menus, line, stripHash: false); break;
                case Mode.Speakers: AddScalar(project.Speakers, line, stripHash: true); break;
                case Mode.Items: AddScalar(project.Items, line, stripHash: true); break;

                case Mode.Descriptions:
                case Mode.Dialogue:
                    if (line.StartsWith('#'))
                    {
                        if (inChoices && LooksLikeChoiceLine(line))
                        {
                            // a self-contained choice option: "#id : text"
                            FlushUnit();
                            var (cid, ctext) = ParseChoiceLine(line);
                            textTarget?.Add(new TextUnit
                            {
                                Id = cid,
                                Annotation = "",
                                Source = [],
                                Target = ctext.Length > 0 ? [ctext] : [],
                                IsChoice = true,
                                ChoiceGroupStart = choiceGroupStart,
                            });
                            choiceGroupStart = false;
                        }
                        else
                        {
                            // a normal entry header "#id (name)" ends any choice run
                            inChoices = false;
                            FlushUnit();
                            var (id, annotation) = ParseTextHeader(line);
                            currentUnit = new TextUnit { Id = id, Annotation = annotation, Source = [], Target = [] };
                        }
                    }
                    else if (line.StartsWith(':') && currentUnit is not null)
                    {
                        currentUnit.Target.Add(ParseTextLine(line));
                    }
                    break;
            }
        }

        FlushUnit();
        return project;
    }

    private static void AddScalar(List<ScalarUnit> target, string line, bool stripHash)
    {
        if (!TrySplitScalar(line, out var key, out var value))
            return;
        if (stripHash && key.StartsWith('#'))
            key = key[1..];
        target.Add(new ScalarUnit { Key = key, Source = "", Target = value });
    }

    private static bool TrySplitScalar(string line, out string key, out string value)
    {
        int colon = line.IndexOf(':');
        if (colon < 0)
        {
            key = value = "";
            return false;
        }

        key = line[..colon].Trim();
        var rest = line[(colon + 1)..];
        value = rest.StartsWith(' ') ? rest[1..] : rest;
        return true;
    }

    /// <summary>True for a self-contained choice line "#id : text" (colon right after the id token).</summary>
    private static bool LooksLikeChoiceLine(string line)
    {
        var rest = line[1..]; // drop '#'
        int space = rest.IndexOf(' ');
        return space >= 0 && rest[(space + 1)..].StartsWith(':');
    }

    private static (string id, string text) ParseChoiceLine(string line)
    {
        var rest = line[1..]; // drop '#'
        int colon = rest.IndexOf(':');
        var id = rest[..colon].Trim();
        var value = rest[(colon + 1)..];
        return (id, value.StartsWith(' ') ? value[1..] : value);
    }

    private static (string id, string annotation) ParseTextHeader(string line)
    {
        var rest = line[1..]; // drop '#'
        int space = rest.IndexOf(' ');
        if (space < 0)
            return (rest, "");

        var id = rest[..space];
        var after = rest[(space + 1)..].Trim();
        var annotation = after.StartsWith('(') && after.EndsWith(')') ? after[1..^1] : after;
        return (id, annotation);
    }

    private static string ParseTextLine(string line)
    {
        // line starts with ':'; content is what follows, minus one optional leading space
        var rest = line[1..];
        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        if (text.StartsWith('﻿'))
            text = text[1..];
        return text.Split('\n').Select(l => l.EndsWith('\r') ? l[..^1] : l);
    }
}
