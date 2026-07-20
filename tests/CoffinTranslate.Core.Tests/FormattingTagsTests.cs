using CoffinTranslate.Core.Project;

namespace CoffinTranslate.Core.Tests;

public class FormattingTagsTests
{
    [Fact]
    public void Extracts_all_code_kinds()
    {
        var tags = FormattingTags.Extract(@"\fi hi \fr\c[1] big\{ small\} \fb");
        Assert.Equal([@"\fi", @"\fr", @"\c[1]", @"\{", @"\}", @"\fb"], tags);
    }

    [Fact]
    public void Empty_target_is_consistent()
    {
        Assert.True(FormattingTags.Consistent(@"\c[1]hello", ""));
    }

    [Fact]
    public void Same_codes_in_any_order_are_consistent()
    {
        Assert.True(FormattingTags.Consistent(@"\c[1]\fi text", @"\fi other\c[1]"));
    }

    [Fact]
    public void Missing_or_extra_code_is_inconsistent()
    {
        Assert.False(FormattingTags.Consistent(@"\c[1]hi", "hi"));
        Assert.False(FormattingTags.Consistent("hi", @"\c[1]hi"));
        Assert.False(FormattingTags.Consistent(@"\c[1]hi", @"\c[2]hi")); // different colour
    }

    [Fact]
    public void Plain_text_is_consistent()
    {
        Assert.True(FormattingTags.Consistent("Hello there", "Hallo da"));
    }
}
