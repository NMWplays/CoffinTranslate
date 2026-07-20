using CoffinTranslate.Core.Project;

namespace CoffinTranslate.Core.Tests;

public class DialogueTxtTests
{
    private static TranslationProject TranslatedSample()
    {
        var p = new TranslationProject
        {
            LanguageName = "Deutsch",
            FontFace = "MyFont.ttf",
            FontSize = 26,
            Credits = ["Alice", "Bob", ""],
            SourceVersionHash = "3.0.13 : a671af9ea6c1a302da7f",
        };
        p.Labels.Add(new ScalarUnit { Key = "Game", Source = "The Coffin", Target = "Der Sarg" });
        p.Menus.Add(new ScalarUnit { Key = "New Game", Source = "New Game", Target = "Neues Spiel" });
        p.Speakers.Add(new ScalarUnit { Key = "t1mR4QYN", Source = "TV", Target = "Fernseher" });
        p.Items.Add(new ScalarUnit { Key = "WjQC7gwG", Source = "Mop", Target = "Wischmopp" });
        p.Descriptions.Add(new TextUnit
        {
            Id = "WjQC7gwG",
            Annotation = "Mop",
            Source = ["Cleans floors"],
            Target = ["Reinigt Böden", "Zweite Zeile"],
        });

        var file = new DialogueFile { FileName = "CommonEvents.json" };
        file.Entries.Add(new TextUnit
        {
            Id = "MT195j9V",
            Annotation = "TV",
            Source = ["\"Some of you maaaaay have heard:\""],
            Target = ["\\c[4]\"Hallo, Welt!\"", "", "Mit Komma, und \\fr Reset"],
        });
        // a two-option choice menu followed by a single-option one
        file.Entries.Add(new TextUnit { Id = "aa11", Source = ["Yes"], Target = ["Ja"], IsChoice = true, ChoiceGroupStart = true });
        file.Entries.Add(new TextUnit { Id = "bb22", Source = ["No"], Target = ["Nein"], IsChoice = true });
        file.Entries.Add(new TextUnit { Id = "cc33", Source = ["Again"], Target = ["Nochmal"], IsChoice = true, ChoiceGroupStart = true });
        p.Dialogue.Add(file);
        return p;
    }

    [Fact]
    public void Writes_expected_header_shape()
    {
        var text = DialogueTxtWriter.Write(TranslatedSample());

        Assert.StartsWith("[VERSION]\n3.0.13 : a671af9ea6c1a302da7f\n\n[LANGUAGE]\nDeutsch\n", text);
        Assert.Contains("[FONT]\nFile : MyFont.ttf\nSize : 26\n", text);
        Assert.Contains("[CREDITS]\n1 : Alice\n2 : Bob\n3 : \n", text);
        Assert.Contains("[SPEAKERS]\n#t1mR4QYN : Fernseher\n", text);
        Assert.Contains("#MT195j9V (TV)\n: \\c[4]\"Hallo, Welt!\"\n: \n: Mit Komma, und \\fr Reset\n", text);
    }

    [Fact]
    public void Aligns_colons_within_a_scalar_section()
    {
        var p = new TranslationProject { LanguageName = "X" };
        p.Menus.Add(new ScalarUnit { Key = "New Game", Target = "Neues Spiel" });
        p.Menus.Add(new ScalarUnit { Key = "On", Target = "An" });

        var text = DialogueTxtWriter.Write(p);

        // both colons align to the longest key ("New Game", 8 chars)
        Assert.Contains("New Game : Neues Spiel\nOn       : An\n", text);
    }

    [Fact]
    public void Empty_value_keeps_the_official_trailing_space()
    {
        // the official tool writes "key : " (with a trailing space) for empty values
        var text = DialogueTxtWriter.Write(TranslatedSample());
        Assert.Contains("3 : \n", text);
    }

    [Fact]
    public void Writes_choices_as_choices_blocks()
    {
        var text = DialogueTxtWriter.Write(TranslatedSample());

        // one [CHOICES] header per menu; both options of the first menu share it
        Assert.Contains("[CHOICES]\n#aa11 : Ja\n#bb22 : Nein\n", text);
        // the single-option menu gets its own header
        Assert.Contains("[CHOICES]\n#cc33 : Nochmal\n", text);
    }

    [Fact]
    public void Untranslated_units_fall_back_to_source()
    {
        var p = new TranslationProject { LanguageName = "X" };
        p.Speakers.Add(new ScalarUnit { Key = "abc", Source = "Andrew", Target = "" });
        var file = new DialogueFile { FileName = "Map001.json" };
        file.Entries.Add(new TextUnit { Id = "xyz", Annotation = "Andrew", Source = ["Hello"], Target = [] });
        file.Entries.Add(new TextUnit { Id = "q1", Source = ["Pick me"], Target = [], IsChoice = true, ChoiceGroupStart = true });
        p.Dialogue.Add(file);

        var text = DialogueTxtWriter.Write(p);

        Assert.Contains("#abc : Andrew", text);
        Assert.Contains("#xyz (Andrew)\n: Hello\n", text);
        Assert.Contains("[CHOICES]\n#q1 : Pick me\n", text); // untranslated choice falls back to English too
    }

    [Fact]
    public void Round_trips_a_translated_project_including_version_and_choices()
    {
        var original = TranslatedSample();
        var reparsed = DialogueTxtReader.Read(DialogueTxtWriter.Write(original));

        Assert.Equal("Deutsch", reparsed.LanguageName);
        Assert.Equal("MyFont.ttf", reparsed.FontFace);
        Assert.Equal(26, reparsed.FontSize);
        Assert.Equal(["Alice", "Bob", ""], reparsed.Credits);
        Assert.Equal("3.0.13 : a671af9ea6c1a302da7f", reparsed.SourceVersionHash);

        Assert.Equal("Der Sarg", Single(reparsed.Labels, "Game"));
        Assert.Equal("Neues Spiel", Single(reparsed.Menus, "New Game"));
        Assert.Equal("Fernseher", Single(reparsed.Speakers, "t1mR4QYN"));
        Assert.Equal("Wischmopp", Single(reparsed.Items, "WjQC7gwG"));

        var desc = Assert.Single(reparsed.Descriptions);
        Assert.Equal("WjQC7gwG", desc.Id);
        Assert.Equal(["Reinigt Böden", "Zweite Zeile"], desc.Target);

        var dlg = Assert.Single(reparsed.Dialogue);
        Assert.Equal("CommonEvents.json", dlg.FileName);
        Assert.Equal(4, dlg.Entries.Count);

        var unit = dlg.Entries[0];
        Assert.Equal("MT195j9V", unit.Id);
        Assert.False(unit.IsChoice);
        Assert.Equal(["\\c[4]\"Hallo, Welt!\"", "", "Mit Komma, und \\fr Reset"], unit.Target);

        // choices survive with their grouping intact
        Assert.True(dlg.Entries[1] is { IsChoice: true, ChoiceGroupStart: true, Id: "aa11" });
        Assert.Equal(["Ja"], dlg.Entries[1].Target);
        Assert.True(dlg.Entries[2] is { IsChoice: true, ChoiceGroupStart: false, Id: "bb22" });
        Assert.True(dlg.Entries[3] is { IsChoice: true, ChoiceGroupStart: true, Id: "cc33" });
    }

    [Fact]
    public void Reader_tolerates_crlf_and_bom()
    {
        var text = "﻿[LANGUAGE]\r\nDeutsch\r\n\r\n[ITEMS]\r\n#abc : Wert\r\n";
        var project = DialogueTxtReader.Read(text);

        Assert.Equal("Deutsch", project.LanguageName);
        Assert.Equal("Wert", Single(project.Items, "abc"));
    }

    private static string Single(List<ScalarUnit> units, string key) => units.Single(u => u.Key == key).Target;
}
