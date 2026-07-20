using CoffinTranslate.Core.IO;

namespace CoffinTranslate.Core.Tests;

public class NameUtilsTests
{
    [Theory]
    [InlineData("Deutsch", "Deutsch")]
    [InlineData("  Deutsch  ", "Deutsch")]
    [InlineData("a/b\\c", "a_b_c")]
    [InlineData("name.", "name")]
    [InlineData("..", "")]
    [InlineData("de:utsch", "de_utsch")]
    public void Sanitizes_names(string input, string expected)
    {
        Assert.Equal(expected, NameUtils.SanitizeFileName(input));
    }
}
