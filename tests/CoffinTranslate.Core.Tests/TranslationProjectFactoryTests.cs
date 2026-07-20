using CoffinTranslate.Core.Project;
using CoffinTranslate.Core.SourceData;

namespace CoffinTranslate.Core.Tests;

public class TranslationProjectFactoryTests
{
    private static GameSourceCatalog Catalog() => new()
    {
        VersionHash = "3.0.13 : abc",
        LanguageName = "English",
        FontFace = "GameFont",
        FontSize = 28,
        SystemLabels = new Dictionary<string, string> { ["Game"] = "The Coffin" },
        Menus = new Dictionary<string, string> { ["New Game"] = "New Game" },
        Speakers = new Dictionary<string, string> { ["t1mR4QYN"] = "TV", ["kR5Fy4cp"] = "Andrew" },
        Items = new Dictionary<string, string> { ["WjQC7gwG"] = "Mop" },
        Texts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["WjQC7gwG"] = ["Cleans floors"],       // item description
            ["MT195j9V"] = ["line one", "line two"], // dialogue
            ["opt1"] = ["Left"],                     // choice options
            ["opt2"] = ["Right"],
            ["narr"] = ["A room."],                  // narration (no speaker)
        },
        Sections =
        [
            new DialogueSection("CommonEvents.json",
            [
                new DialogueEntry("t1mR4QYN", ["MT195j9V"]),
                new DialogueEntry("", ["narr"]),                                     // narrator
                new DialogueEntry(TranslationProjectFactory.ChoiceSpeaker, ["opt1", "opt2"]), // a 2-option menu
            ]),
        ],
    };

    [Fact]
    public void Builds_all_sections_from_catalog()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());

        Assert.Single(p.Labels);
        Assert.Single(p.Menus);
        Assert.Equal(2, p.Speakers.Count);
        Assert.Single(p.Items);
        Assert.Single(p.Descriptions);
        Assert.Single(p.Dialogue);
        Assert.Equal("GameFont", p.FontFace);
        Assert.Equal(28, p.FontSize);
    }

    [Fact]
    public void Source_is_populated_but_target_is_empty()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());

        var speaker = p.Speakers.Single(u => u.Key == "t1mR4QYN");
        Assert.Equal("TV", speaker.Source);
        Assert.Equal("", speaker.Target);
        Assert.False(speaker.IsTranslated);
    }

    [Fact]
    public void Dialogue_entry_pulls_source_lines_and_speaker_annotation()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());

        var unit = p.Dialogue.Single().Entries[0];
        Assert.Equal("MT195j9V", unit.Id);
        Assert.Equal("TV", unit.Annotation);
        Assert.Equal(["line one", "line two"], unit.Source);
        Assert.Empty(unit.Target);
    }

    [Fact]
    public void Narrator_entry_is_labelled_Narrator()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());

        var narr = p.Dialogue.Single().Entries.Single(e => e.Id == "narr");
        Assert.Equal("Narrator", narr.Annotation);
        Assert.False(narr.IsChoice);
    }

    [Fact]
    public void Choice_entry_becomes_grouped_choice_options()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());
        var entries = p.Dialogue.Single().Entries;

        var opt1 = entries.Single(e => e.Id == "opt1");
        var opt2 = entries.Single(e => e.Id == "opt2");
        Assert.True(opt1 is { IsChoice: true, ChoiceGroupStart: true });
        Assert.Equal(["Left"], opt1.Source);
        Assert.True(opt2 is { IsChoice: true, ChoiceGroupStart: false }); // second option of the same menu
        Assert.Equal(["Right"], opt2.Source);
    }

    [Fact]
    public void Description_shares_item_id_and_pulls_text()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());

        var desc = p.Descriptions.Single();
        Assert.Equal("WjQC7gwG", desc.Id);
        Assert.Equal("Mop", desc.Annotation);
        Assert.Equal(["Cleans floors"], desc.Source);
    }

    [Fact]
    public void Fresh_project_reports_zero_translated()
    {
        var p = TranslationProjectFactory.FromCatalog(Catalog());
        Assert.DoesNotContain(p.AllUnits(), u => u.IsTranslated);
    }
}
