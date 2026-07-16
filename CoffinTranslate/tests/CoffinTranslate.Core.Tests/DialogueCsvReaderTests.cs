using CoffinTranslate.Core.Project;

namespace CoffinTranslate.Core.Tests;

public class DialogueCsvReaderTests
{
    // Mirrors the official dialogue.csv layout (docs/official-tool-analysis.md §3.2).
    private const string Sample =
        "Version,,,\n" +
        "3.0.13 : a671af9ea6c1a302da7f,,,\n" +
        ",,,\n" +
        "Language,Font File,Font Size,\n" +
        "Deutsch,MyFont.ttf,26,\n" +
        ",,,\n" +
        "Credit 1,Credit 2,Credit 3,\n" +
        "Alice,Bob,,\n" +
        ",,,\n" +
        "Labels,English,Translation,\n" +
        "Game,The Coffin,Der Sarg,\n" +
        ",,,\n" +
        "Menus,Translation,,\n" +
        "New Game,Neues Spiel,,\n" +
        ",,,\n" +
        "Speakers,English,Translation,\n" +
        "t1mR4QYN,TV,Fernseher,\n" +
        ",,,\n" +
        "Items,English,Translation,\n" +
        "WjQC7gwG,Mop,Wischmopp,\n" +
        ",,,\n" +
        "Descriptions,Item,English,Translation\n" +
        "WjQC7gwG,Mop,Cleans floors,Reinigt Böden\n" +
        ",,,\n" +
        "Section,CommonEvents.json,,\n" +
        "ID,Source,English,Translation\n" +
        "MT195j9V,TV,\"\"\"Line one\"\"\",Zeile eins\n" +
        "MT195j9V,TV,\"and line two\"\"\",und Zeile zwei\n" +
        "narr,Narrator,A room.,Ein Raum.\n" +
        "D0xbRbb2,CHOICE(1),Left Arm.,Linker Arm.\n" +
        "RD9G4nvv,CHOICE(2),Right Arm.,Rechter Arm.\n" +
        "ZWb8r3qD,CHOICE(1),Call again.,\n"; // untranslated single-option menu

    private static TranslationProject Read() => DialogueCsvReader.Read(Sample);

    [Fact]
    public void Reads_metadata_font_credits_and_version()
    {
        var p = Read();
        Assert.Equal("Deutsch", p.LanguageName);
        Assert.Equal("MyFont.ttf", p.FontFace);
        Assert.Equal(26, p.FontSize);
        Assert.Equal(["Alice", "Bob", ""], p.Credits);
        Assert.Equal("3.0.13 : a671af9ea6c1a302da7f", p.SourceVersionHash);
    }

    [Fact]
    public void Reads_scalar_sections_with_source_and_target()
    {
        var p = Read();

        var label = Assert.Single(p.Labels);
        Assert.Equal("Game", label.Key);
        Assert.Equal("The Coffin", label.Source);
        Assert.Equal("Der Sarg", label.Target);

        var menu = Assert.Single(p.Menus);
        Assert.Equal("New Game", menu.Key);
        Assert.Equal("New Game", menu.Source); // the key IS the English text
        Assert.Equal("Neues Spiel", menu.Target);

        Assert.Equal("Fernseher", p.Speakers.Single(u => u.Key == "t1mR4QYN").Target);
        Assert.Equal("Wischmopp", p.Items.Single(u => u.Key == "WjQC7gwG").Target);
    }

    [Fact]
    public void Groups_consecutive_same_id_rows_into_one_multiline_entry()
    {
        var p = Read();
        var entry = p.Dialogue.Single().Entries.First(e => e.Id == "MT195j9V");

        Assert.Equal("TV", entry.Annotation);
        Assert.Equal(["\"Line one\"", "and line two\""], entry.Source);
        Assert.Equal(["Zeile eins", "und Zeile zwei"], entry.Target);
        Assert.False(entry.IsChoice);
    }

    [Fact]
    public void Narrator_row_keeps_its_annotation()
    {
        var narr = Read().Dialogue.Single().Entries.Single(e => e.Id == "narr");
        Assert.Equal("Narrator", narr.Annotation);
        Assert.Equal(["Ein Raum."], narr.Target);
    }

    [Fact]
    public void Choice_rows_become_grouped_options_via_choice_index()
    {
        var entries = Read().Dialogue.Single().Entries;

        var left = entries.Single(e => e.Id == "D0xbRbb2");
        var right = entries.Single(e => e.Id == "RD9G4nvv");
        var again = entries.Single(e => e.Id == "ZWb8r3qD");

        // CHOICE(1)/CHOICE(2) form one menu; the group starts at index 1
        Assert.True(left is { IsChoice: true, ChoiceGroupStart: true });
        Assert.Equal(["Linker Arm."], left.Target);
        Assert.True(right is { IsChoice: true, ChoiceGroupStart: false });

        // a following CHOICE(1) starts a new (single-option) menu, left untranslated
        Assert.True(again is { IsChoice: true, ChoiceGroupStart: true });
        Assert.Empty(again.Target); // empty translation stays genuinely empty
        Assert.False(again.IsTranslated);
    }

    [Fact]
    public void Csv_import_then_txt_export_produces_valid_choices()
    {
        var text = DialogueTxtWriter.Write(Read());
        Assert.Contains("[CHOICES]\n#D0xbRbb2 : Linker Arm.\n#RD9G4nvv : Rechter Arm.\n", text);
        Assert.Contains("[CHOICES]\n#ZWb8r3qD : Call again.\n", text); // untranslated falls back to English
    }
}
