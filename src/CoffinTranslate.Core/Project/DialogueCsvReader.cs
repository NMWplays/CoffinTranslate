using System.Text.RegularExpressions;
using CoffinTranslate.Core.Parsing;

namespace CoffinTranslate.Core.Project;

/// <summary>
/// Parses the official tool's <c>dialogue.csv</c> into a <see cref="TranslationProject"/>. Unlike the
/// TXT format, the CSV keeps English and Translation in separate columns, so this reader fills both
/// <c>Source</c> and <c>Target</c> — an untranslated line stays genuinely empty instead of showing
/// English as if it were a translation. Layout is documented in docs/official-tool-analysis.md §3.2.
/// </summary>
public static partial class DialogueCsvReader
{
    public static TranslationProject Read(string text)
    {
        var rows = CsvReader.Parse(text);
        var project = new TranslationProject { FontFace = "", FontSize = 0, Credits = ["", "", ""] };

        string block = "";                       // active content block
        DialogueFile? file = null;               // active [Section]
        List<TextUnit>? textTarget = null;       // Descriptions or the active file's entries

        // pending multi-line normal entry (dialogue/description lines share an id across rows)
        string pendId = "", pendAnn = "";
        List<string>? pendSrc = null, pendTgt = null;

        void FlushPending()
        {
            if (pendSrc is null || textTarget is null)
                return;
            textTarget.Add(new TextUnit { Id = pendId, Annotation = pendAnn, Source = pendSrc, Target = pendTgt! });
            pendSrc = null;
            pendTgt = null;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (IsBlank(row))
            {
                FlushPending();
                block = "";
                continue;
            }

            var head = Cell(row, 0).Trim();
            var col1 = Cell(row, 1).Trim();

            // --- metadata + block headers ---
            if (head == "Version") { project.SourceVersionHash = ValueRow(rows, ref i, 0); continue; }
            if (head == "Language" && col1 == "Font File")
            {
                var v = i + 1 < rows.Count ? rows[i + 1] : [];
                project.LanguageName = Cell(v, 0);
                project.FontFace = Cell(v, 1);
                project.FontSize = int.TryParse(Cell(v, 2).Trim(), out var s) ? s : 0;
                i++;
                continue;
            }
            if (head == "Credit 1")
            {
                var v = i + 1 < rows.Count ? rows[i + 1] : [];
                project.Credits = [Cell(v, 0), Cell(v, 1), Cell(v, 2)];
                i++;
                continue;
            }
            if (head == "Labels" && col1 == "English") { FlushPending(); block = "Labels"; continue; }
            if (head == "Menus" && col1 == "Translation") { FlushPending(); block = "Menus"; continue; }
            if (head == "Speakers" && col1 == "English") { FlushPending(); block = "Speakers"; continue; }
            if (head == "Items" && col1 == "English") { FlushPending(); block = "Items"; continue; }
            if (head == "Descriptions" && col1 == "Item")
            {
                FlushPending();
                block = "Descriptions";
                textTarget = project.Descriptions;
                continue;
            }
            if (head == "Section")
            {
                FlushPending();
                block = "Section";
                file = new DialogueFile { FileName = col1 };
                project.Dialogue.Add(file);
                textTarget = file.Entries;
                continue;
            }

            // the "ID, Source, English, Translation" sub-header inside a [Section]
            if (block == "Section" && head == "ID" && col1 == "Source")
                continue;

            // --- data rows ---
            switch (block)
            {
                case "Labels":
                    project.Labels.Add(new ScalarUnit { Key = head, Source = Cell(row, 1), Target = Cell(row, 2) });
                    break;
                case "Menus":
                    // key IS the English text; only key + translation columns
                    project.Menus.Add(new ScalarUnit { Key = head, Source = head, Target = Cell(row, 1) });
                    break;
                case "Speakers":
                    project.Speakers.Add(new ScalarUnit { Key = head, Source = Cell(row, 1), Target = Cell(row, 2) });
                    break;
                case "Items":
                    project.Items.Add(new ScalarUnit { Key = head, Source = Cell(row, 1), Target = Cell(row, 2) });
                    break;
                case "Descriptions":
                    AppendTextRow(id: head, annotation: Cell(row, 1), english: Cell(row, 2), translation: Cell(row, 3));
                    break;
                case "Section":
                    ReadSectionRow(row);
                    break;
            }

            continue;

            // dialogue rows: choices (speaker "CHOICE(n)") are single-line options; everything
            // else groups consecutive same-id rows into one multi-line entry
            void ReadSectionRow(string[] r)
            {
                var id = Cell(r, 0);
                var speaker = Cell(r, 1);
                var english = Cell(r, 2);
                var translation = Cell(r, 3);

                var choice = ChoiceIndex(speaker);
                if (choice > 0)
                {
                    FlushPending();
                    textTarget?.Add(new TextUnit
                    {
                        Id = id,
                        Annotation = "",
                        Source = english.Length > 0 ? [english] : [],
                        Target = translation.Length > 0 ? [translation] : [],
                        IsChoice = true,
                        ChoiceGroupStart = choice == 1,
                    });
                }
                else
                {
                    AppendTextRow(id, speaker, english, translation);
                }
            }

            void AppendTextRow(string id, string annotation, string english, string translation)
            {
                if (pendSrc is not null && pendId == id)
                {
                    pendSrc.Add(english);
                    pendTgt!.Add(translation);
                    return;
                }

                FlushPending();
                pendId = id;
                pendAnn = annotation;
                pendSrc = [english];
                pendTgt = [translation];
            }
        }

        FlushPending();
        return project;
    }

    private static string ValueRow(List<string[]> rows, ref int i, int col)
    {
        var value = i + 1 < rows.Count ? Cell(rows[i + 1], col) : "";
        i++;
        return value;
    }

    /// <summary>Returns the 1-based option index n for a "CHOICE(n)" speaker, or 0 if it isn't one.</summary>
    private static int ChoiceIndex(string speaker)
    {
        var m = ChoiceRegex().Match(speaker);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    private static bool IsBlank(string[] row) => row.All(c => c.Trim().Length == 0);

    private static string Cell(string[] row, int index) => index < row.Length ? row[index] : "";

    [GeneratedRegex(@"^CHOICE\((\d+)\)$")]
    private static partial Regex ChoiceRegex();
}
