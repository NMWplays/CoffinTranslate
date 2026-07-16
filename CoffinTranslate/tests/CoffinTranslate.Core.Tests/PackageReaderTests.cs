using System.IO.Compression;
using CoffinTranslate.Core;
using CoffinTranslate.Core.Packages;

namespace CoffinTranslate.Core.Tests;

public class PackageReaderTests
{
    private const string DialogueTxt = """
        [LANGUAGE]
        Deutsch

        [FONT]
        File : GameFont
        Size : 28

        [CREDITS]
        1 : Alice

        [LABELS]
        Game : X
        """;

    [Fact]
    public void Reads_folder_with_dialogue_at_root()
    {
        using var temp = new TempDir();
        var root = temp.CreateDir("Deutsch");
        File.WriteAllText(Path.Combine(root, "dialogue.txt"), DialogueTxt);

        var package = PackageReader.Read(root);

        Assert.Equal(PackageSourceKind.Folder, package.Kind);
        Assert.Equal(DialogueFormat.Txt, package.Format);
        Assert.Equal("Deutsch", package.InstallName);
        Assert.Equal("Deutsch", package.Metadata?.LanguageName);
        Assert.Empty(package.Warnings);
    }

    [Fact]
    public void Reads_nested_folder_and_uses_inner_folder_as_root()
    {
        using var temp = new TempDir();
        temp.WriteFile("download/extracted/German v2/dialogue.csv", "Language,Font File,Font Size\nDeutsch,GameFont,28\n");
        temp.WriteFile("download/extracted/German v2/pictures/a.png", "png");
        temp.WriteFile("download/extracted/German v2/font/custom.ttf", "font");

        var package = PackageReader.Read(temp.Combine("download"));

        Assert.Equal(DialogueFormat.Csv, package.Format);
        Assert.Equal("German v2", package.InstallName);
        Assert.EndsWith("German v2", package.ContentRootPath);
        Assert.True(package.HasFont);
        Assert.Equal(1, package.ImageCount);
    }

    [Fact]
    public void Reading_the_dialogue_file_itself_analyzes_its_folder()
    {
        using var temp = new TempDir();
        var file = temp.WriteFile("Deutsch/dialogue.txt", DialogueTxt);

        var package = PackageReader.Read(file);

        Assert.Equal(PackageSourceKind.Folder, package.Kind);
        Assert.Equal("Deutsch", package.InstallName);
    }

    [Fact]
    public void Folder_without_translation_throws_NoDialogueFileFound()
    {
        using var temp = new TempDir();
        temp.WriteFile("readme.md", "nothing here");

        var ex = Assert.Throws<PackageReadException>(() => PackageReader.Read(temp.Path));
        Assert.Equal(PackageReadError.NoDialogueFileFound, ex.Error);
    }

    [Fact]
    public void Folder_with_cld_file_is_read_as_cld_package()
    {
        using var temp = new TempDir();
        var cld = temp.WriteFile("Deutsch.cld", "binary");

        var package = PackageReader.Read(temp.Path);

        Assert.Equal(PackageSourceKind.CldFile, package.Kind);
        Assert.Equal(DialogueFormat.Cld, package.Format);
        Assert.Equal("Deutsch.cld", package.InstallName);
        Assert.Equal(Path.GetFullPath(cld), package.ContentRootPath);
        Assert.Equal("Deutsch", package.DisplayLanguage);
    }

    [Fact]
    public void Reads_zip_with_root_folder_prefix()
    {
        using var temp = new TempDir();
        var zipPath = temp.Combine("paket.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "Deutsch/dialogue.txt", DialogueTxt);
            WriteEntry(zip, "Deutsch/font/custom.ttf", "font");
            WriteEntry(zip, "Deutsch/pictures/a.png", "png");
            WriteEntry(zip, "Deutsch/system/b.png", "png");
        }

        var package = PackageReader.Read(zipPath);

        Assert.Equal(PackageSourceKind.Archive, package.Kind);
        Assert.Equal("Deutsch/", package.ArchiveRootPrefix);
        Assert.Equal("Deutsch", package.InstallName);
        Assert.Equal("Deutsch", package.Metadata?.LanguageName);
        Assert.True(package.HasFont);
        Assert.Equal(2, package.ImageCount);
    }

    [Fact]
    public void Reads_flat_zip_and_names_it_after_the_archive()
    {
        using var temp = new TempDir();
        var zipPath = temp.Combine("Meine Übersetzung.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            WriteEntry(zip, "dialogue.txt", DialogueTxt);

        var package = PackageReader.Read(zipPath);

        Assert.Equal("", package.ArchiveRootPrefix);
        Assert.Equal("Meine Übersetzung", package.InstallName);
    }

    [Fact]
    public void Zip_with_multiple_translations_warns_and_uses_shallowest()
    {
        using var temp = new TempDir();
        var zipPath = temp.Combine("multi.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "a/deep/dialogue.txt", DialogueTxt);
            WriteEntry(zip, "b/dialogue.txt", DialogueTxt);
        }

        var package = PackageReader.Read(zipPath);

        Assert.Equal("b/", package.ArchiveRootPrefix);
        Assert.Contains(package.Warnings, w => w.Code == PackageWarningCode.MultipleTranslationsFound);
    }

    [Fact]
    public void Zip_containing_only_cld_is_supported()
    {
        using var temp = new TempDir();
        var zipPath = temp.Combine("cld.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            WriteEntry(zip, "release/Deutsch.cld", "binary");

        var package = PackageReader.Read(zipPath);

        Assert.Equal(PackageSourceKind.Archive, package.Kind);
        Assert.Equal(DialogueFormat.Cld, package.Format);
        Assert.Equal("Deutsch.cld", package.InstallName);
        Assert.Equal("release/Deutsch.cld", package.ArchiveRootPrefix);
    }

    [Fact]
    public void Rar_archives_are_rejected_with_specific_error()
    {
        using var temp = new TempDir();
        var rar = temp.WriteFile("paket.rar", "not really a rar");

        var ex = Assert.Throws<PackageReadException>(() => PackageReader.Read(rar));
        Assert.Equal(PackageReadError.UnsupportedArchiveType, ex.Error);
    }

    [Fact]
    public void Missing_path_throws_PathNotFound()
    {
        var ex = Assert.Throws<PackageReadException>(() => PackageReader.Read(Path.Combine(Path.GetTempPath(), "does-not-exist-xyz")));
        Assert.Equal(PackageReadError.PathNotFound, ex.Error);
    }

    [Fact]
    public void Invalid_utf8_dialogue_produces_encoding_warning()
    {
        using var temp = new TempDir();
        var root = temp.CreateDir("Deutsch");
        // Latin-1 encoded umlaut is invalid UTF-8
        File.WriteAllBytes(Path.Combine(root, "dialogue.txt"), [.. "[LANGUAGE]\n"u8.ToArray(), 0xE4, (byte)'\n']);

        var package = PackageReader.Read(root);

        Assert.Contains(package.Warnings, w => w.Code == PackageWarningCode.EncodingProblems);
    }

    internal static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
