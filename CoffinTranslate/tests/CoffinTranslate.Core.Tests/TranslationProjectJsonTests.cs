using CoffinTranslate.Core.Project;

namespace CoffinTranslate.Core.Tests;

public class TranslationProjectJsonTests
{
    private static TranslationProject SampleProject()
    {
        var project = new TranslationProject
        {
            LanguageName = "Deutsch",
            FontFace = "MyFont.ttf",
            FontFilePath = @"C:\fonts\MyFont.ttf",
            FontSize = 26,
            Credits = ["Alice", "Bob", ""],
            SourceVersionHash = "3.0.13 : abcdef",
        };
        project.Labels.Add(new ScalarUnit { Key = "Game", Source = "The Coffin", Target = "Der Sarg" });
        project.Labels.Add(new ScalarUnit { Key = "Item", Source = "Item", Target = "" }); // untranslated

        var file = new DialogueFile { FileName = "CommonEvents.json" };
        file.Entries.Add(new TextUnit
        {
            Id = "MT195j9V",
            Annotation = "TV",
            Source = ["first line", "second line"],
            Target = ["erste Zeile", "zweite Zeile"],
        });
        file.Entries.Add(new TextUnit
        {
            Id = "F6mK5ZQ0",
            Annotation = "",
            Source = ["You're feeling dizzy..."],
            Target = [], // untranslated
        });
        project.Dialogue.Add(file);

        project.ImageReplacements["img/pictures/abc123"] = @"C:\repl\title.png";
        return project;
    }

    [Fact]
    public void Roundtrips_all_fields()
    {
        var restored = TranslationProjectJson.Read(TranslationProjectJson.Write(SampleProject()));

        Assert.Equal("Deutsch", restored.LanguageName);
        Assert.Equal("MyFont.ttf", restored.FontFace);
        Assert.Equal(@"C:\fonts\MyFont.ttf", restored.FontFilePath);
        Assert.Equal(26, restored.FontSize);
        Assert.Equal(["Alice", "Bob", ""], restored.Credits);
        Assert.Equal("3.0.13 : abcdef", restored.SourceVersionHash);

        Assert.Equal("Der Sarg", restored.Labels[0].Target);
        Assert.Equal("The Coffin", restored.Labels[0].Source);

        var entry = restored.Dialogue[0].Entries[0];
        Assert.Equal(["erste Zeile", "zweite Zeile"], entry.Target);
        Assert.Equal(["first line", "second line"], entry.Source);

        Assert.Equal(@"C:\repl\title.png", restored.ImageReplacements["img/pictures/abc123"]);
    }

    [Fact]
    public void Preserves_untranslated_units_as_empty()
    {
        // The whole point of the native format: reopening must not present English as a translation.
        var restored = TranslationProjectJson.Read(TranslationProjectJson.Write(SampleProject()));

        Assert.False(restored.Labels[1].IsTranslated);   // "Item" left blank
        Assert.Equal("", restored.Labels[1].Target);

        var untranslated = restored.Dialogue[0].Entries[1];
        Assert.False(untranslated.IsTranslated);
        Assert.Empty(untranslated.Target);
        Assert.Equal(["You're feeling dizzy..."], untranslated.Source); // source is still there to translate against
    }

    [Fact]
    public void Contrasts_with_dialogue_txt_which_fills_english()
    {
        // dialogue.txt export fills untranslated lines with English (playable), which is exactly
        // why we need a separate native format for resuming work.
        var project = SampleProject();
        var txt = DialogueTxtWriter.Write(project);
        Assert.Contains("You're feeling dizzy...", txt); // English fallback present in the game file

        var resumed = TranslationProjectJson.Read(TranslationProjectJson.Write(project));
        Assert.False(resumed.Dialogue[0].Entries[1].IsTranslated); // but the project keeps it open
    }
}
