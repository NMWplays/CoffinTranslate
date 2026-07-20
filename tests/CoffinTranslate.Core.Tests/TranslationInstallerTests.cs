using System.IO.Compression;
using CoffinTranslate.Core;
using CoffinTranslate.Core.Game;
using CoffinTranslate.Core.Install;
using CoffinTranslate.Core.Packages;

namespace CoffinTranslate.Core.Tests;

public class TranslationInstallerTests : IDisposable
{
    private const string DialogueTxt = """
        [LANGUAGE]
        Deutsch

        [LABELS]
        Game : X
        """;

    private readonly TempDir _temp = new();
    private readonly GameInstallation _game;
    private readonly TranslationInstaller _installer;

    public TranslationInstallerTests()
    {
        _temp.WriteFile("game/www/index.html", "<html></html>");
        _temp.WriteFile("game/www/languages/tool/Translator.dat", "official");
        _game = new GameInstallation(_temp.Combine("game"), GameSource.Manual);
        _installer = new TranslationInstaller(_temp.Combine("backups"));
    }

    public void Dispose() => _temp.Dispose();

    private TranslationPackage FolderPackage(string name = "Deutsch")
    {
        _temp.WriteFile($"source/{name}/dialogue.txt", DialogueTxt);
        _temp.WriteFile($"source/{name}/font/custom.ttf", "font");
        return PackageReader.Read(_temp.Combine("source", name));
    }

    [Fact]
    public void Installs_folder_package()
    {
        var result = _installer.Install(_game, FolderPackage());

        Assert.False(result.ReplacedExisting);
        Assert.Null(result.BackupPath);
        Assert.True(File.Exists(Path.Combine(_game.LanguagesPath, "Deutsch", "dialogue.txt")));
        Assert.True(File.Exists(Path.Combine(_game.LanguagesPath, "Deutsch", "font", "custom.ttf")));
    }

    [Fact]
    public void Reinstall_backs_up_the_existing_translation()
    {
        var package = FolderPackage();
        _installer.Install(_game, package);
        File.WriteAllText(Path.Combine(_game.LanguagesPath, "Deutsch", "marker.txt"), "old version");

        var result = _installer.Install(_game, package);

        Assert.True(result.ReplacedExisting);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(Path.Combine(result.BackupPath, "marker.txt")));
        Assert.False(File.Exists(Path.Combine(_game.LanguagesPath, "Deutsch", "marker.txt")));
    }

    [Fact]
    public void Reinstall_without_backup_replaces_silently()
    {
        var package = FolderPackage();
        _installer.Install(_game, package);

        var result = _installer.Install(_game, package, backupExisting: false);

        Assert.True(result.ReplacedExisting);
        Assert.Null(result.BackupPath);
        Assert.False(Directory.Exists(_temp.Combine("backups")));
    }

    [Fact]
    public void Installs_zip_package()
    {
        var zipPath = _temp.Combine("paket.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            PackageReaderTests.WriteEntry(zip, "Deutsch/dialogue.txt", DialogueTxt);
            PackageReaderTests.WriteEntry(zip, "Deutsch/pictures/a.png", "png");
        }

        var result = _installer.Install(_game, PackageReader.Read(zipPath));

        Assert.True(File.Exists(Path.Combine(result.InstalledPath, "dialogue.txt")));
        Assert.True(File.Exists(Path.Combine(result.InstalledPath, "pictures", "a.png")));
    }

    [Fact]
    public void Zip_slip_entries_abort_the_install()
    {
        var zipPath = _temp.Combine("evil.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            PackageReaderTests.WriteEntry(zip, "dialogue.txt", DialogueTxt);
            PackageReaderTests.WriteEntry(zip, "../evil.txt", "escape!");
        }

        var package = PackageReader.Read(zipPath);
        var ex = Assert.Throws<InstallException>(() => _installer.Install(_game, package));

        Assert.Equal(InstallErrorCode.ArchiveEntryOutsideTarget, ex.Code);
        Assert.False(File.Exists(Path.Combine(_game.LanguagesPath, "..", "evil.txt")));
    }

    [Fact]
    public void Installs_cld_file()
    {
        var cld = _temp.WriteFile("Deutsch.cld", "binary");

        var result = _installer.Install(_game, PackageReader.Read(cld));

        Assert.Equal(Path.Combine(_game.LanguagesPath, "Deutsch.cld"), result.InstalledPath);
        Assert.True(File.Exists(result.InstalledPath));
    }

    [Fact]
    public void Installs_cld_from_zip()
    {
        var zipPath = _temp.Combine("cld.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            PackageReaderTests.WriteEntry(zip, "release/Deutsch.cld", "binary");

        var result = _installer.Install(_game, PackageReader.Read(zipPath));

        Assert.Equal("binary", File.ReadAllText(Path.Combine(_game.LanguagesPath, "Deutsch.cld")));
        Assert.Equal(Path.Combine(_game.LanguagesPath, "Deutsch.cld"), result.InstalledPath);
    }

    [Fact]
    public void Tool_name_is_reserved()
    {
        var package = new TranslationPackage
        {
            SourcePath = _temp.Path,
            ContentRootPath = _temp.Path,
            Kind = PackageSourceKind.Folder,
            Format = DialogueFormat.Txt,
            InstallName = "tool",
        };

        var ex = Assert.Throws<InstallException>(() => _installer.Install(_game, package));
        Assert.Equal(InstallErrorCode.ReservedName, ex.Code);
        Assert.True(File.Exists(Path.Combine(_game.OfficialToolPath, "Translator.dat")));
    }

    [Fact]
    public void Invalid_game_folder_aborts()
    {
        var bogusGame = new GameInstallation(_temp.Combine("nope"), GameSource.Manual);

        var ex = Assert.Throws<InstallException>(() => _installer.Install(bogusGame, FolderPackage()));
        Assert.Equal(InstallErrorCode.GameFolderMissing, ex.Code);
    }

    [Fact]
    public void Scanner_lists_installed_translations_but_never_the_tool()
    {
        _installer.Install(_game, FolderPackage());
        var cld = _temp.WriteFile("English Redux.cld", "binary");
        _installer.Install(_game, PackageReader.Read(cld));
        _temp.WriteFile("game/www/languages/broken/readme.txt", "no dialogue here");

        var installed = InstalledTranslationScanner.Scan(_game);

        Assert.Equal(3, installed.Count);
        Assert.DoesNotContain(installed, t => t.Name.Equals("tool", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(installed, t => t is { Name: "Deutsch", Kind: InstalledKind.Folder, Metadata.LanguageName: "Deutsch" });
        Assert.Contains(installed, t => t is { Name: "English Redux.cld", Kind: InstalledKind.CldFile, DisplayLanguage: "English Redux" });
        Assert.Contains(installed, t => t is { Name: "broken", IsRecognized: false });
    }

    [Fact]
    public void Uninstall_moves_translation_to_backup()
    {
        _installer.Install(_game, FolderPackage());
        var installed = Assert.Single(InstalledTranslationScanner.Scan(_game), t => t.Name == "Deutsch");

        var backupPath = _installer.Uninstall(installed);

        Assert.NotNull(backupPath);
        Assert.False(Directory.Exists(installed.FullPath));
        Assert.True(File.Exists(Path.Combine(backupPath, "dialogue.txt")));
    }

    [Fact]
    public void Uninstall_without_backup_deletes()
    {
        _installer.Install(_game, FolderPackage());
        var installed = Assert.Single(InstalledTranslationScanner.Scan(_game), t => t.Name == "Deutsch");

        var backupPath = _installer.Uninstall(installed, backup: false);

        Assert.Null(backupPath);
        Assert.False(Directory.Exists(installed.FullPath));
    }

    [Fact]
    public void Missing_languages_folder_is_created_on_install()
    {
        _temp.WriteFile("fresh/www/index.html", "<html></html>");
        var freshGame = new GameInstallation(_temp.Combine("fresh"), GameSource.Manual);

        var result = new TranslationInstaller(_temp.Combine("backups")).Install(freshGame, FolderPackage());

        Assert.True(File.Exists(Path.Combine(result.InstalledPath, "dialogue.txt")));
    }
}
