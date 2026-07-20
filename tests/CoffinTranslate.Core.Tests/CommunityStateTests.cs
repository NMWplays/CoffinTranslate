using CoffinTranslate.Core.Community;

namespace CoffinTranslate.Core.Tests;

public class CommunityVersionTests
{
    [Theory]
    [InlineData("1.3", "1.2", 1)]
    [InlineData("1.2", "1.3", -1)]
    [InlineData("1.2", "1.2", 0)]
    [InlineData("1.10", "1.9", 1)]   // numeric, not lexical
    [InlineData("2", "1.9", 1)]
    [InlineData("1", "1.0.0", 0)]    // shorter padded with zeros
    [InlineData("1.0.1", "1", 1)]
    public void Compares_dotted_numeric_versions(string a, string b, int expected)
    {
        Assert.Equal(expected, CommunityVersion.Compare(a, b));
    }

    [Fact]
    public void IsNewer_is_strict()
    {
        Assert.True(CommunityVersion.IsNewer("1.3", "1.2"));
        Assert.False(CommunityVersion.IsNewer("1.2", "1.2"));
        Assert.False(CommunityVersion.IsNewer("1.1", "1.2"));
    }

    [Fact]
    public void Non_numeric_segment_falls_back_to_ordinal()
    {
        // does not throw; produces a total order
        Assert.NotEqual(0, CommunityVersion.Compare("1.3b", "1.3a"));
    }
}

public class CommunityInstallStateTests
{
    private static CommunityPack Pack(string id, string version) =>
        new() { Id = id, Language = "Deutsch", Version = version, File = "packs/de.zip" };

    [Fact]
    public void No_ledger_record_means_not_installed()
    {
        var state = CommunityInstall.DetermineState(Pack("de", "1.0"), record: null, installedFolderNames: ["Deutsch"]);
        Assert.Equal(PackInstallState.NotInstalled, state);
    }

    [Fact]
    public void Record_but_folder_removed_means_not_installed()
    {
        var record = new InstalledPackRecord("Deutsch", "1.0");
        var state = CommunityInstall.DetermineState(Pack("de", "1.0"), record, installedFolderNames: ["Français"]);
        Assert.Equal(PackInstallState.NotInstalled, state);
    }

    [Fact]
    public void Same_version_installed()
    {
        var record = new InstalledPackRecord("Deutsch", "1.0");
        var state = CommunityInstall.DetermineState(Pack("de", "1.0"), record, installedFolderNames: ["Deutsch"]);
        Assert.Equal(PackInstallState.Installed, state);
    }

    [Fact]
    public void Newer_catalog_version_offers_update()
    {
        var record = new InstalledPackRecord("Deutsch", "1.0");
        var state = CommunityInstall.DetermineState(Pack("de", "1.2"), record, installedFolderNames: ["Deutsch"]);
        Assert.Equal(PackInstallState.UpdateAvailable, state);
    }

    [Fact]
    public void Folder_name_match_is_case_insensitive()
    {
        var record = new InstalledPackRecord("Deutsch", "1.0");
        var state = CommunityInstall.DetermineState(Pack("de", "1.0"), record, installedFolderNames: ["deutsch"]);
        Assert.Equal(PackInstallState.Installed, state);
    }
}

public class InstalledPackLedgerTests
{
    [Fact]
    public void Records_and_reads_back_across_instances()
    {
        using var temp = new TempDir();
        var path = temp.Combine("installed-packs.json");

        var ledger = new InstalledPackLedger(path);
        ledger.Record("deutsch-marie", "Deutsch", "1.3");

        // a fresh instance must load what was persisted
        var reloaded = new InstalledPackLedger(path);
        var record = reloaded.Get("deutsch-marie");
        Assert.NotNull(record);
        Assert.Equal("Deutsch", record!.InstallName);
        Assert.Equal("1.3", record.Version);
    }

    [Fact]
    public void Record_overwrites_previous_version()
    {
        using var temp = new TempDir();
        var path = temp.Combine("ledger.json");
        var ledger = new InstalledPackLedger(path);

        ledger.Record("de", "Deutsch", "1.0");
        ledger.Record("de", "Deutsch", "1.4");

        Assert.Equal("1.4", ledger.Get("de")!.Version);
    }

    [Fact]
    public void Remove_forgets_a_pack()
    {
        using var temp = new TempDir();
        var path = temp.Combine("ledger.json");
        var ledger = new InstalledPackLedger(path);
        ledger.Record("de", "Deutsch", "1.0");

        ledger.Remove("de");

        Assert.Null(ledger.Get("de"));
        Assert.Null(new InstalledPackLedger(path).Get("de"));
    }

    [Fact]
    public void Missing_file_is_treated_as_empty()
    {
        using var temp = new TempDir();
        var ledger = new InstalledPackLedger(temp.Combine("does-not-exist.json"));
        Assert.Null(ledger.Get("anything"));
    }

    [Fact]
    public void Corrupt_file_is_treated_as_empty()
    {
        using var temp = new TempDir();
        var path = temp.WriteFile("ledger.json", "{ this is not valid json");

        var ledger = new InstalledPackLedger(path);

        Assert.Null(ledger.Get("de"));
    }
}
