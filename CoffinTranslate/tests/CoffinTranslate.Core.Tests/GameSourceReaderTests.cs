using System.IO.Compression;
using CoffinTranslate.Core.SourceData;

namespace CoffinTranslate.Core.Tests;

public class GameSourceReaderTests
{
    private static IReadOnlyDictionary<object, object?> SampleRoot(object? fontData = null) => Py.Dict(
        ("ver_hash", "3.0.13 : a671af9ea6c1a302da7f"),
        ("lng_name", "English"),
        ("lng_info", Py.List("Alice", "Bob", "")),
        ("fnt_face", "GameFont"),
        ("fnt_size", 28),
        ("fnt_data", fontData),
        ("sys_lbls", Py.Dict(("Game", "The Coffin of Andy and Leyley"), ("Item", "Item"))),
        ("sys_menu", Py.Dict(("New Game", "New Game"), ("Continue", "Continue"))),
        ("actr_lut", Py.Dict(("t1mR4QYN", "TV"), ("kR5Fy4cp", "Andrew"))),
        ("item_lut", Py.Dict(("WjQC7gwG", "Mop"))),
        ("text_lut", Py.Dict(
            ("F6mK5ZQ0", Py.List("You're feeling dizzy...")),
            ("MT195j9V", Py.List("first line", "second line")),
            ("WjQC7gwG", Py.List("")))),
        ("sections", Py.Dict(
            ("CommonEvents.json", Py.List(
                Py.Dict(("name", ""), ("text", Py.List("F6mK5ZQ0"))),
                Py.Dict(("name", "t1mR4QYN"), ("text", Py.List("MT195j9V"))))),
            ("Map001.json", Py.List(
                Py.Dict(("name", "kR5Fy4cp"), ("text", Py.List("F6mK5ZQ0"))))))),
        ("img_data", Py.Dict(("img/pictures/abc123", new byte[] { 1, 2, 3 }))));

    private static GameSourceCatalog ReadSample(object? fontData = null) =>
        GameSourceReader.FromPickle(PickleWriter.Pickle(SampleRoot(fontData)));

    [Fact]
    public void Parses_version_and_hash()
    {
        var cat = ReadSample();
        Assert.Equal("3.0.13 : a671af9ea6c1a302da7f", cat.VersionHash);
        Assert.Equal("3.0.13", cat.Version);
        Assert.Equal("a671af9ea6c1a302da7f", cat.DataHash);
    }

    [Fact]
    public void Maps_metadata_and_font()
    {
        var cat = ReadSample();
        Assert.Equal("English", cat.LanguageName);
        Assert.Equal(["Alice", "Bob", ""], cat.Credits);
        Assert.Equal("GameFont", cat.FontFace);
        Assert.Equal(28, cat.FontSize);
        Assert.False(cat.HasEmbeddedFont);
    }

    [Fact]
    public void Detects_embedded_font()
    {
        var cat = ReadSample(fontData: new byte[] { 0, 1, 2 });
        Assert.True(cat.HasEmbeddedFont);
    }

    [Fact]
    public void Maps_lookup_tables()
    {
        var cat = ReadSample();
        Assert.Equal(2, cat.Speakers.Count);
        Assert.Equal("TV", cat.Speakers["t1mR4QYN"]);
        Assert.Equal("Mop", cat.Items["WjQC7gwG"]);
        Assert.Equal(2, cat.Menus.Count);
        Assert.Equal("The Coffin of Andy and Leyley", cat.SystemLabels["Game"]);
    }

    [Fact]
    public void Preserves_multiline_text()
    {
        var cat = ReadSample();
        Assert.Equal(["first line", "second line"], cat.Texts["MT195j9V"]);
        Assert.Equal([""], cat.Texts["WjQC7gwG"]); // empty item description
    }

    [Fact]
    public void Preserves_section_and_entry_order()
    {
        var cat = ReadSample();

        Assert.Equal(["CommonEvents.json", "Map001.json"], cat.Sections.Select(s => s.FileName));

        var common = cat.Sections[0];
        Assert.Equal(2, common.Entries.Count);
        Assert.Equal("", common.Entries[0].SpeakerId);              // narrator
        Assert.Equal("t1mR4QYN", common.Entries[1].SpeakerId);
        Assert.Equal(["F6mK5ZQ0"], common.Entries[0].TextIds);
    }

    [Fact]
    public void Lists_image_paths()
    {
        var cat = ReadSample();
        Assert.Equal(["img/pictures/abc123"], cat.ImagePaths);
    }

    [Fact]
    public void Reads_through_zlib_stream()
    {
        var pickle = PickleWriter.Pickle(SampleRoot());
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(pickle);
        compressed.Position = 0;

        var cat = GameSourceReader.Read(compressed);
        Assert.Equal("3.0.13", cat.Version);
        Assert.Equal(3, cat.Texts.Count);
    }

    [Theory]
    [InlineData("3.0.13 : abcdef", "3.0.13", "abcdef")]
    [InlineData("3.0.13", "3.0.13", null)]
    [InlineData("", null, null)]
    public void Splits_version_hash_variants(string verHash, string? version, string? hash)
    {
        var root = Py.Dict(("ver_hash", verHash), ("lng_name", "English"));
        var cat = GameSourceReader.FromPickle(PickleWriter.Pickle(root));
        Assert.Equal(version, cat.Version);
        Assert.Equal(hash, cat.DataHash);
    }

    [Fact]
    public void Throws_when_root_is_not_a_dict()
    {
        Assert.Throws<GameSourceFormatException>(
            () => GameSourceReader.FromPickle(PickleWriter.Pickle(Py.List("nope"))));
    }
}
