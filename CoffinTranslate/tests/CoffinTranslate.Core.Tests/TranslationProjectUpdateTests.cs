using CoffinTranslate.Core.Project;
using CoffinTranslate.Core.SourceData;

namespace CoffinTranslate.Core.Tests;

public class TranslationProjectUpdateTests
{
    // v1: the version the project was originally built against.
    private static GameSourceCatalog V1() => new()
    {
        VersionHash = "3.0.13 : abc",
        LanguageName = "English",
        FontFace = "GameFont",
        FontSize = 28,
        SystemLabels = new Dictionary<string, string> { ["Game"] = "The Coffin" },
        Menus = new Dictionary<string, string> { ["New Game"] = "New Game" },
        Speakers = new Dictionary<string, string> { ["t1mR4QYN"] = "TV" },
        Items = new Dictionary<string, string> { ["WjQC7gwG"] = "Mop" },
        Texts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["WjQC7gwG"] = ["Cleans floors"],
            ["MT195j9V"] = ["line one", "line two"],
            ["narr"] = ["A room."],
            ["gone"] = ["obsolete line"],
        },
        Sections =
        [
            new DialogueSection("CommonEvents.json",
            [
                new DialogueEntry("t1mR4QYN", ["MT195j9V"]),
                new DialogueEntry("", ["narr"]),
                new DialogueEntry("t1mR4QYN", ["gone"]),
            ]),
        ],
    };

    // v2: a later game version — one label added, the mop description reworded, a new dialogue line,
    // a whole new file, and the "gone" line dropped.
    private static GameSourceCatalog V2() => new()
    {
        VersionHash = "3.0.14 : def",
        LanguageName = "English",
        FontFace = "GameFont",
        FontSize = 28,
        SystemLabels = new Dictionary<string, string> { ["Game"] = "The Coffin", ["Extra"] = "Extra Label" },
        Menus = new Dictionary<string, string> { ["New Game"] = "New Game" },
        Speakers = new Dictionary<string, string> { ["t1mR4QYN"] = "TV" },
        Items = new Dictionary<string, string> { ["WjQC7gwG"] = "Mop" },
        Texts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["WjQC7gwG"] = ["Cleans floors and windows"], // reworded
            ["MT195j9V"] = ["line one", "line two"],       // unchanged
            ["narr"] = ["A room."],                        // unchanged
            ["MT2"] = ["brand new line"],                  // new
            ["MapText"] = ["map line"],                    // new (new file)
        },
        Sections =
        [
            new DialogueSection("CommonEvents.json",
            [
                new DialogueEntry("t1mR4QYN", ["MT195j9V"]),
                new DialogueEntry("", ["narr"]),
                new DialogueEntry("t1mR4QYN", ["MT2"]),
            ]),
            new DialogueSection("Map001.json",
            [
                new DialogueEntry("", ["MapText"]),
            ]),
        ],
    };

    private static (TranslationProject Project, UpdateResult Result) Updated()
    {
        var project = TranslationProjectFactory.FromCatalog(V1());
        // translate a couple of lines so we can prove translations survive the update
        project.Dialogue.Single().Entries.Single(e => e.Id == "MT195j9V").Target = ["übersetzt"];
        project.Labels.Single(u => u.Key == "Game").Target = "Der Sarg";

        var result = TranslationProjectFactory.UpdateFromCatalog(project, V2());
        return (project, result);
    }

    [Fact]
    public void Reports_added_changed_and_removed_counts()
    {
        var (_, result) = Updated();

        Assert.Equal(3, result.Added);   // Extra label + MT2 + MapText
        Assert.Equal(1, result.Changed); // mop description reworded
        Assert.Equal(1, result.Removed); // "gone" dropped
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Keeps_existing_translations()
    {
        var (project, _) = Updated();

        Assert.Equal(["übersetzt"], project.Dialogue.First().Entries.Single(e => e.Id == "MT195j9V").Target);
        Assert.Equal("Der Sarg", project.Labels.Single(u => u.Key == "Game").Target);
    }

    [Fact]
    public void Unchanged_translated_line_is_not_flagged_new()
    {
        var (project, _) = Updated();

        var unit = project.Dialogue.First().Entries.Single(e => e.Id == "MT195j9V");
        Assert.False(unit.IsNew);
    }

    [Fact]
    public void Added_label_is_appended_and_flagged_new()
    {
        var (project, _) = Updated();

        var extra = project.Labels.Single(u => u.Key == "Extra");
        Assert.Equal("Extra Label", extra.Source);
        Assert.True(extra.IsNew);
        Assert.False(extra.IsTranslated);
    }

    [Fact]
    public void Changed_description_source_is_refreshed_and_flagged()
    {
        var (project, _) = Updated();

        var mop = project.Descriptions.Single(u => u.Id == "WjQC7gwG");
        Assert.Equal(["Cleans floors and windows"], mop.Source);
        Assert.True(mop.IsNew);
    }

    [Fact]
    public void New_line_is_added_to_the_right_file_and_flagged()
    {
        var (project, _) = Updated();

        var common = project.Dialogue.Single(f => f.FileName == "CommonEvents.json");
        var mt2 = common.Entries.Single(e => e.Id == "MT2");
        Assert.Equal(["brand new line"], mt2.Source);
        Assert.True(mt2.IsNew);
    }

    [Fact]
    public void New_file_is_created()
    {
        var (project, _) = Updated();

        var map = project.Dialogue.Single(f => f.FileName == "Map001.json");
        var text = map.Entries.Single(e => e.Id == "MapText");
        Assert.True(text.IsNew);
    }

    [Fact]
    public void Dropped_line_is_kept_not_deleted()
    {
        var (project, _) = Updated();

        // "gone" is only counted as removed; it stays in the project so its translation isn't lost
        Assert.Contains(project.Dialogue.First().Entries, e => e.Id == "gone");
    }

    [Fact]
    public void Version_stamp_is_advanced()
    {
        var (project, _) = Updated();
        Assert.Equal("3.0.14 : def", project.SourceVersionHash);
    }

    [Fact]
    public void No_changes_when_updating_against_the_same_catalog()
    {
        var project = TranslationProjectFactory.FromCatalog(V1());
        var result = TranslationProjectFactory.UpdateFromCatalog(project, V1());

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Changed);
        Assert.Equal(0, result.Removed);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void IsNew_and_editing_time_survive_a_json_round_trip()
    {
        var (project, _) = Updated();
        project.EditingSeconds = 4321;

        var restored = TranslationProjectJson.Read(TranslationProjectJson.Write(project));

        Assert.Equal(4321, restored.EditingSeconds);
        Assert.True(restored.Labels.Single(u => u.Key == "Extra").IsNew);
        Assert.True(restored.Descriptions.Single(u => u.Id == "WjQC7gwG").IsNew);
        Assert.False(restored.Dialogue.First().Entries.Single(e => e.Id == "MT195j9V").IsNew);
    }
}
