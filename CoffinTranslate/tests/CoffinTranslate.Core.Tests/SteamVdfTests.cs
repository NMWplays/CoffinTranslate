using CoffinTranslate.Core.Game;

namespace CoffinTranslate.Core.Tests;

public class SteamVdfTests
{
    [Fact]
    public void Extracts_all_library_paths_and_unescapes_backslashes()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "label"		""
                }
                "1"
                {
                    "path"		"E:\\SteamLibrary"
                }
            }
            """;

        var paths = SteamVdf.ParseLibraryPaths(vdf);

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"E:\SteamLibrary"], paths);
    }

    [Fact]
    public void Handles_linux_style_paths()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"/home/user/.local/share/Steam"
                }
            }
            """;

        Assert.Equal(["/home/user/.local/share/Steam"], SteamVdf.ParseLibraryPaths(vdf));
    }

    [Fact]
    public void Empty_content_yields_no_paths()
    {
        Assert.Empty(SteamVdf.ParseLibraryPaths(""));
    }
}
