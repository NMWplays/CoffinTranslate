using CoffinTranslate.Core.Project;

namespace CoffinTranslate.Core.Tests;

public class TranslationImageExporterTests
{
    [Fact]
    public void Writes_replacement_under_folder_and_bare_hash_name()
    {
        using var temp = new TempDir();
        var replacement = temp.WriteFile("mine.png", "PNGDATA");
        var project = new TranslationProject();
        project.ImageReplacements["img/pictures/5972d18a4e7f34a8"] = replacement;
        var outDir = temp.CreateDir("out");

        var missing = TranslationImageExporter.Export(project, outDir);

        Assert.Empty(missing);
        var written = Path.Combine(outDir, "pictures", "5972d18a4e7f34a8");
        Assert.True(File.Exists(written));                       // no "img/" prefix, no extension
        Assert.Equal("PNGDATA", File.ReadAllText(written));
    }

    [Fact]
    public void Reports_missing_source_files()
    {
        using var temp = new TempDir();
        var project = new TranslationProject();
        project.ImageReplacements["img/system/e5230bf37c4fabb0"] = temp.Combine("does-not-exist.png");

        var missing = TranslationImageExporter.Export(project, temp.CreateDir("out"));

        Assert.Equal(["img/system/e5230bf37c4fabb0"], missing);
    }

    [Theory]
    [InlineData("img/pictures/abc", "pictures/abc")]
    [InlineData("img/titles1/xyz", "titles1/xyz")]
    [InlineData("pictures/abc", null)]        // missing img/ prefix
    [InlineData("img/../secret", null)]       // traversal
    [InlineData("img/", null)]                // empty remainder
    public void Maps_key_to_relative_path(string key, string? expected)
    {
        Assert.Equal(expected, TranslationImageExporter.RelativePath(key));
    }
}
