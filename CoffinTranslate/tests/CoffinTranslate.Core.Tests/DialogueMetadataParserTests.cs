using CoffinTranslate.Core;
using CoffinTranslate.Core.Parsing;

namespace CoffinTranslate.Core.Tests;

public class DialogueMetadataParserTests
{
    private const string SampleTxt = """
        [LANGUAGE]
        Deutsch

        [FONT]
        File : MyFont.ttf
        Size : 22

        [CREDITS]
        1 : Alice
        2 : Bob / Carol
        3 :

        [LABELS]
        Game : The Coffin of Andy and Leyley
        """;

    private const string SampleCsv = """
        Language,Font File,Font Size
        Deutsch,GameFont,28

        Credit 1,Credit 2,Credit 3
        "Müller, A.",Bob,

        Labels,English,Translation
        Game,The Coffin of Andy and Leyley,Der Sarg
        Language,SHOULD NOT BE PICKED UP,1
        """;

    [Fact]
    public void Parses_txt_header()
    {
        var metadata = DialogueMetadataParser.Parse(SampleTxt, DialogueFormat.Txt);

        Assert.Equal("Deutsch", metadata.LanguageName);
        Assert.Equal("MyFont.ttf", metadata.FontFile);
        Assert.Equal(22, metadata.FontSize);
        Assert.Equal(["Alice", "Bob / Carol"], metadata.Credits);
    }

    [Fact]
    public void Parses_csv_header_including_quoted_credits()
    {
        var metadata = DialogueMetadataParser.Parse(SampleCsv, DialogueFormat.Csv);

        Assert.Equal("Deutsch", metadata.LanguageName);
        Assert.Equal("GameFont", metadata.FontFile);
        Assert.Equal(28, metadata.FontSize);
        Assert.Equal(["Müller, A.", "Bob"], metadata.Credits);
    }

    [Fact]
    public void Txt_stops_at_content_sections()
    {
        var metadata = DialogueMetadataParser.Parse("[LABELS]\nGame : X\n[LANGUAGE]\nTrap", DialogueFormat.Txt);

        Assert.Null(metadata.LanguageName);
    }

    [Fact]
    public void Empty_content_yields_empty_metadata()
    {
        var metadata = DialogueMetadataParser.Parse("", DialogueFormat.Txt);

        Assert.Null(metadata.LanguageName);
        Assert.Empty(metadata.Credits);
    }

    [Fact]
    public void Cld_format_yields_empty_metadata()
    {
        var metadata = DialogueMetadataParser.Parse("anything", DialogueFormat.Cld);

        Assert.Same(TranslationMetadata.Empty, metadata);
    }
}
