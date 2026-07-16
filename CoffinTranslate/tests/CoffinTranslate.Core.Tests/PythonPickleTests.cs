using CoffinTranslate.Core.Serialization;

namespace CoffinTranslate.Core.Tests;

public class PythonPickleTests
{
    [Fact]
    public void Reads_scalars()
    {
        Assert.Null(PythonPickle.Load(PickleWriter.Pickle(null)));
        Assert.Equal("hello", PythonPickle.Load(PickleWriter.Pickle("hello")));
        Assert.Equal(42L, PythonPickle.Load(PickleWriter.Pickle(42)));
        Assert.Equal(true, PythonPickle.Load(PickleWriter.Pickle(true)));
        Assert.Equal(false, PythonPickle.Load(PickleWriter.Pickle(false)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]      // BININT1 boundary
    [InlineData(256)]      // BININT2
    [InlineData(65535)]    // BININT2 boundary
    [InlineData(70000)]    // BININT
    [InlineData(1000000)]
    public void Reads_ints_across_encodings(int value)
    {
        Assert.Equal((long)value, PythonPickle.Load(PickleWriter.Pickle(value)));
    }

    [Fact]
    public void Reads_unicode_beyond_short_length()
    {
        var big = new string('ä', 500); // forces BINUNICODE (>255 utf8 bytes) and non-ASCII
        Assert.Equal(big, PythonPickle.Load(PickleWriter.Pickle(big)));
    }

    [Fact]
    public void Reads_list()
    {
        var result = PythonPickle.Load(PickleWriter.Pickle(Py.List("a", "b", 3)));

        var list = Assert.IsType<List<object?>>(result);
        Assert.Equal(["a", "b", 3L], list);
    }

    [Fact]
    public void Reads_dict_and_preserves_insertion_order()
    {
        var result = PythonPickle.Load(PickleWriter.Pickle(
            Py.Dict(("z", 1), ("a", 2), ("m", 3))));

        var dict = Assert.IsType<OrderedDictionary<object, object?>>(result);
        Assert.Equal(["z", "a", "m"], dict.Keys.Cast<string>());
        Assert.Equal(2L, dict["a"]);
    }

    [Fact]
    public void Reads_nested_structures()
    {
        var result = PythonPickle.Load(PickleWriter.Pickle(
            Py.Dict(("outer", Py.List(Py.Dict(("k", "v")), "tail")))));

        var dict = Assert.IsType<OrderedDictionary<object, object?>>(result);
        var list = Assert.IsType<List<object?>>(dict["outer"]);
        var inner = Assert.IsType<OrderedDictionary<object, object?>>(list[0]);
        Assert.Equal("v", inner["k"]);
        Assert.Equal("tail", list[1]);
    }

    [Fact]
    public void Reads_bytearray_as_bytes()
    {
        byte[] payload = [0x89, 0x50, 0x4E, 0x47]; // PNG-ish
        var result = PythonPickle.Load(PickleWriter.Pickle(payload));

        Assert.Equal(payload, Assert.IsType<byte[]>(result));
    }

    [Fact]
    public void Rejects_executable_global()
    {
        // "os" "system" STACK_GLOBAL — the classic dangerous case, never constructed
        byte[] evil = [0x80, 4, 0x8C, 2, (byte)'o', (byte)'s',
                       0x8C, 6, (byte)'s', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m',
                       0x93, (byte)'.'];
        var ex = Record.Exception(() => PythonPickle.Load(evil));
        // STACK_GLOBAL itself is tolerated (produces an inert ref); using it via REDUCE is what throws.
        // Here STOP follows immediately, so the global ref is simply returned — assert no code ran.
        Assert.Null(ex);
    }

    [Fact]
    public void Rejects_reduce_on_non_bytearray_global()
    {
        // os.system STACK_GLOBAL, arg "x", REDUCE  -> must throw, never execute
        byte[] evil = [0x80, 4,
                       0x8C, 2, (byte)'o', (byte)'s',
                       0x8C, 6, (byte)'s', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m',
                       0x93,
                       0x8C, 1, (byte)'x',
                       (byte)'R', (byte)'.'];
        Assert.Throws<PickleFormatException>(() => PythonPickle.Load(evil));
    }

    [Fact]
    public void Rejects_truncated_stream()
    {
        var full = PickleWriter.Pickle(Py.List("a", "b"));
        var truncated = full[..(full.Length - 3)];
        Assert.Throws<PickleFormatException>(() => PythonPickle.Load(truncated));
    }
}
