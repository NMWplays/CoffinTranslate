using CoffinTranslate.Core.Community;

namespace CoffinTranslate.Core.Tests;

public class CommunityCatalogTests
{
    private const string CatalogUrl = "https://raw.githubusercontent.com/NMWplays/CoffinTranslate/main/catalog.json";

    [Fact]
    public void Parses_a_full_catalog()
    {
        const string json = """
        {
          "schema": 1,
          "packs": [
            {
              "id": "deutsch-marie",
              "language": "Deutsch",
              "authors": "Marie",
              "version": "1.3",
              "gameVersion": "3.0.13",
              "description": "Vollständige deutsche Übersetzung.",
              "source": "https://steamcommunity.com/x",
              "file": "packs/deutsch.zip"
            }
          ]
        }
        """;

        var catalog = CommunityCatalogParser.Parse(json);

        Assert.Equal(1, catalog.Schema);
        var pack = Assert.Single(catalog.Packs);
        Assert.Equal("deutsch-marie", pack.Id);
        Assert.Equal("Deutsch", pack.Language);
        Assert.Equal("Marie", pack.Authors);
        Assert.Equal("1.3", pack.Version);
        Assert.Equal("3.0.13", pack.GameVersion);
        Assert.Equal("packs/deutsch.zip", pack.File);
        Assert.Equal("https://steamcommunity.com/x", pack.Source);
    }

    [Fact]
    public void Optional_fields_default_and_do_not_break_parsing()
    {
        const string json = """
        { "packs": [ { "id": "x", "language": "Français", "version": "1", "file": "packs/fr.zip" } ] }
        """;

        var catalog = CommunityCatalogParser.Parse(json);

        var pack = Assert.Single(catalog.Packs);
        Assert.Equal("", pack.Authors);
        Assert.Null(pack.GameVersion);
        Assert.Null(pack.Description);
        Assert.Null(pack.Source);
        Assert.Equal(1, catalog.Schema); // defaulted when absent
    }

    [Fact]
    public void Skips_entries_missing_required_fields_but_keeps_valid_ones()
    {
        const string json = """
        {
          "packs": [
            { "language": "NoId", "version": "1", "file": "a.zip" },
            { "id": "b", "language": "Good", "version": "2", "file": "b.zip" },
            { "id": "c", "language": "NoFile", "version": "3" }
          ]
        }
        """;

        var catalog = CommunityCatalogParser.Parse(json);

        var pack = Assert.Single(catalog.Packs);
        Assert.Equal("b", pack.Id);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        const string json = """
        { "packs": [ { "id": "x", "language": "L", "version": "1", "file": "x.zip", "somethingNew": 42 } ] }
        """;

        var pack = Assert.Single(CommunityCatalogParser.Parse(json).Packs);
        Assert.Equal("x", pack.Id);
    }

    [Fact]
    public void Invalid_json_throws()
    {
        Assert.Throws<CommunityCatalogException>(() => CommunityCatalogParser.Parse("not json"));
        Assert.Throws<CommunityCatalogException>(() => CommunityCatalogParser.Parse("[1,2,3]"));
    }

    [Fact]
    public void Missing_packs_array_yields_empty_catalog()
    {
        var catalog = CommunityCatalogParser.Parse("""{ "schema": 1 }""");
        Assert.Empty(catalog.Packs);
    }

    [Fact]
    public void Resolves_relative_file_against_catalog_url()
    {
        var url = CommunityCatalog.ResolveFileUrl(CatalogUrl, "packs/deutsch.zip");
        Assert.Equal("https://raw.githubusercontent.com/NMWplays/CoffinTranslate/main/packs/deutsch.zip", url);
    }

    [Fact]
    public void Absolute_file_url_passes_through()
    {
        var url = CommunityCatalog.ResolveFileUrl(CatalogUrl, "https://github.com/NMWplays/CoffinTranslate/releases/download/v1/deutsch.zip");
        Assert.Equal("https://github.com/NMWplays/CoffinTranslate/releases/download/v1/deutsch.zip", url);
    }
}
