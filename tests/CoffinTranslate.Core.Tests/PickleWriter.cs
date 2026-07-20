using System.Text;

namespace CoffinTranslate.Core.Tests;

/// <summary>
/// Tiny protocol-4 pickle writer used only to build hermetic fixtures for the reader tests.
/// Emits the same opcode subset the reader accepts. Not a general-purpose pickler.
/// </summary>
internal sealed class PickleWriter
{
    private readonly MemoryStream _s = new();

    public static byte[] Pickle(object? value)
    {
        var w = new PickleWriter();
        w._s.WriteByte(0x80); w._s.WriteByte(4);     // PROTO 4
        w.Write(value);
        w._s.WriteByte((byte)'.');                    // STOP
        return w._s.ToArray();
    }

    private void Write(object? value)
    {
        switch (value)
        {
            case null: _s.WriteByte((byte)'N'); break;
            case bool b: _s.WriteByte(b ? (byte)0x88 : (byte)0x89); break;
            case string s: WriteStr(s); break;
            case int i: WriteInt(i); break;
            case long l: WriteInt(checked((int)l)); break;
            case byte[] bytes: WriteByteArray(bytes); break;
            case IReadOnlyList<object?> list: WriteList(list); break;
            case IReadOnlyDictionary<object, object?> dict: WriteDict(dict); break;
            default: throw new NotSupportedException($"PickleWriter cannot write {value.GetType()}");
        }
    }

    private void WriteStr(string s)
    {
        var utf8 = Encoding.UTF8.GetBytes(s);
        if (utf8.Length < 256)
        {
            _s.WriteByte(0x8C);                        // SHORT_BINUNICODE
            _s.WriteByte((byte)utf8.Length);
        }
        else
        {
            _s.WriteByte((byte)'X');                   // BINUNICODE
            _s.Write(BitConverter.GetBytes(utf8.Length));
        }
        _s.Write(utf8);
    }

    private void WriteInt(int i)
    {
        if (i is >= 0 and < 256) { _s.WriteByte((byte)'K'); _s.WriteByte((byte)i); }
        else if (i is >= 0 and < 65536) { _s.WriteByte((byte)'M'); _s.Write(BitConverter.GetBytes((ushort)i)); }
        else { _s.WriteByte((byte)'J'); _s.Write(BitConverter.GetBytes(i)); }
    }

    private void WriteByteArray(byte[] bytes)
    {
        WriteStr("builtins");
        WriteStr("bytearray");
        _s.WriteByte(0x93);                            // STACK_GLOBAL
        if (bytes.Length < 256) { _s.WriteByte(0xC4); _s.WriteByte((byte)bytes.Length); } // SHORT_BINBYTES
        else { _s.WriteByte((byte)'B'); _s.Write(BitConverter.GetBytes(bytes.Length)); }  // BINBYTES
        _s.Write(bytes);
        _s.WriteByte((byte)'R');                        // REDUCE
    }

    private void WriteList(IReadOnlyList<object?> list)
    {
        _s.WriteByte((byte)']');                        // EMPTY_LIST
        _s.WriteByte((byte)'(');                        // MARK
        foreach (var item in list) Write(item);
        _s.WriteByte((byte)'e');                        // APPENDS
    }

    private void WriteDict(IReadOnlyDictionary<object, object?> dict)
    {
        _s.WriteByte((byte)'}');                        // EMPTY_DICT
        _s.WriteByte((byte)'(');                        // MARK
        foreach (var (k, v) in dict) { Write(k); Write(v); }
        _s.WriteByte((byte)'u');                        // SETITEMS
    }

}

// Convenience builders so tests read cleanly.
internal static class Py
{
    public static IReadOnlyList<object?> List(params object?[] items) => items;

    public static IReadOnlyDictionary<object, object?> Dict(params (string key, object? value)[] pairs)
    {
        var d = new Dictionary<object, object?>();
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }
}
