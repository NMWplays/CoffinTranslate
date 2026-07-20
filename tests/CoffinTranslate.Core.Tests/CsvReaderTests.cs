using CoffinTranslate.Core.Parsing;

namespace CoffinTranslate.Core.Tests;

public class CsvReaderTests
{
    [Fact]
    public void Parses_simple_rows()
    {
        var rows = CsvReader.Parse("a,b,c\nd,e,f");

        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b", "c"], rows[0]);
        Assert.Equal(["d", "e", "f"], rows[1]);
    }

    [Fact]
    public void Handles_quoted_fields_with_commas()
    {
        var rows = CsvReader.Parse("\"a,b\",c");

        Assert.Single(rows);
        Assert.Equal(["a,b", "c"], rows[0]);
    }

    [Fact]
    public void Handles_escaped_quotes()
    {
        var rows = CsvReader.Parse("\"He said \"\"hi\"\"\",x");

        Assert.Equal(["He said \"hi\"", "x"], rows[0]);
    }

    [Fact]
    public void Handles_newlines_inside_quoted_fields()
    {
        var rows = CsvReader.Parse("\"line1\r\nline2\",b");

        Assert.Single(rows);
        Assert.Equal("line1\r\nline2", rows[0][0]);
        Assert.Equal("b", rows[0][1]);
    }

    [Theory]
    [InlineData("a,b\r\nc,d")]
    [InlineData("a,b\nc,d")]
    [InlineData("a,b\rc,d")]
    public void Handles_all_line_ending_styles(string text)
    {
        var rows = CsvReader.Parse(text);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b"], rows[0]);
        Assert.Equal(["c", "d"], rows[1]);
    }

    [Fact]
    public void Trailing_newline_does_not_create_empty_row()
    {
        var rows = CsvReader.Parse("a,b\n");

        Assert.Single(rows);
    }

    [Fact]
    public void Empty_line_between_blocks_becomes_single_empty_field()
    {
        var rows = CsvReader.Parse("a\n\nb");

        Assert.Equal(3, rows.Count);
        Assert.Equal([""], rows[1]);
    }
}
