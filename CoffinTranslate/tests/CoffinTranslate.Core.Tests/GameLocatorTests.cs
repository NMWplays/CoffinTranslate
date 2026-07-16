using CoffinTranslate.Core.Game;

namespace CoffinTranslate.Core.Tests;

public class GameLocatorTests
{
    [Fact]
    public void Recognizes_valid_game_folder()
    {
        using var temp = new TempDir();
        temp.WriteFile("www/index.html", "<html></html>");

        Assert.True(GameLocator.LooksLikeGameFolder(temp.Path));
    }

    [Fact]
    public void Rejects_folder_without_www()
    {
        using var temp = new TempDir();

        Assert.False(GameLocator.LooksLikeGameFolder(temp.Path));
        Assert.Null(GameLocator.TryCreateManual(temp.Path));
    }

    [Fact]
    public void Manual_selection_of_www_folder_normalizes_to_root()
    {
        using var temp = new TempDir();
        temp.WriteFile("www/index.html", "<html></html>");

        var installation = GameLocator.TryCreateManual(temp.Combine("www"));

        Assert.NotNull(installation);
        Assert.Equal(Path.GetFullPath(temp.Path), installation.RootPath);
        Assert.Equal(GameSource.Manual, installation.Source);
    }

    [Fact]
    public void Paths_derive_from_root()
    {
        var installation = new GameInstallation(@"C:\game", GameSource.Manual);

        Assert.Equal(Path.Combine(@"C:\game", "www", "languages"), installation.LanguagesPath);
        Assert.Equal(Path.Combine(@"C:\game", "www", "languages", "tool"), installation.OfficialToolPath);
    }
}
