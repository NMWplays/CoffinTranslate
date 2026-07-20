using System.Text;

namespace CoffinTranslate.Core.Serialization;

/// <summary>
/// A deliberately restricted, <b>data-only</b> reader for Python pickles (protocols 2–5).
///
/// <para>Python's own <c>pickle</c> can execute arbitrary code on load. This reader cannot:
/// it implements only the opcodes needed for plain data (dict, list, tuple, set, str, bytes,
/// int, float, bool, None). The single global it honours is <c>builtins.bytearray</c>, which
/// it turns into a <see cref="T:byte[]"/>. Any other <c>GLOBAL</c>/<c>REDUCE</c>/<c>BUILD</c>/
/// <c>NEWOBJ</c> — i.e. anything that could construct an object or run code — throws
/// <see cref="PickleFormatException"/>.</para>
///
/// <para>Result object model: dicts become <see cref="OrderedDictionary{TKey,TValue}"/> (insertion
/// order preserved), lists and sets become <see cref="List{T}"/>, tuples become <c>object?[]</c>,
/// and scalars become <see cref="string"/>, <see cref="long"/>, <see cref="double"/>,
/// <see cref="bool"/>, <see cref="T:byte[]"/> or <see langword="null"/>.</para>
/// </summary>
public static class PythonPickle
{
    public static object? Load(byte[] data) => new Reader(data).Load();

    private sealed class Reader(byte[] data)
    {
        private int _pos;
        private readonly List<object?> _stack = [];
        private readonly List<int> _marks = [];
        private readonly Dictionary<int, object?> _memo = [];

        public object? Load()
        {
            try
            {
                while (true)
                {
                    byte op = data[_pos++];
                    switch (op)
                    {
                        case 0x80: _pos++; break;                              // PROTO
                        case 0x95: _pos += 8; break;                           // FRAME
                        case (byte)'.': return _stack.Count > 0 ? _stack[^1] : null; // STOP
                        case (byte)'(': _marks.Add(_stack.Count); break;       // MARK
                        case (byte)'}': Push(new OrderedDictionary<object, object?>()); break; // EMPTY_DICT
                        case (byte)']': Push(new List<object?>()); break;      // EMPTY_LIST
                        case (byte)')': Push(Array.Empty<object?>()); break;   // EMPTY_TUPLE
                        case 0x8F: Push(new List<object?>()); break;           // EMPTY_SET (as list)
                        case 0x94: _memo[_memo.Count] = _stack[^1]; break;     // MEMOIZE
                        case (byte)'q': _memo[data[_pos++]] = _stack[^1]; break;          // BINPUT
                        case (byte)'r': _memo[ReadInt32()] = _stack[^1]; break;           // LONG_BINPUT
                        case (byte)'h': Push(_memo[data[_pos++]]); break;                 // BINGET
                        case (byte)'j': Push(_memo[ReadInt32()]); break;                  // LONG_BINGET
                        case 0x8C: Push(ReadStr(data[_pos++])); break;                    // SHORT_BINUNICODE
                        case (byte)'X': Push(ReadStr(ReadInt32())); break;                // BINUNICODE
                        case 0x8D: Push(ReadStr(checked((int)ReadUInt64()))); break;      // BINUNICODE8
                        case 0xC4: Push(ReadBytes(data[_pos++])); break;                  // SHORT_BINBYTES
                        case (byte)'B': Push(ReadBytes(ReadInt32())); break;              // BINBYTES
                        case 0x8E: Push(ReadBytes(checked((int)ReadUInt64()))); break;    // BINBYTES8
                        case (byte)'N': Push(null); break;                               // NONE
                        case 0x88: Push(true); break;                                    // NEWTRUE
                        case 0x89: Push(false); break;                                   // NEWFALSE
                        case (byte)'K': Push((long)data[_pos++]); break;                  // BININT1
                        case (byte)'M': Push((long)(data[_pos] | (data[_pos + 1] << 8))); _pos += 2; break; // BININT2
                        case (byte)'J': Push((long)ReadInt32()); break;                   // BININT
                        case 0x8A: Push(ReadLong(data[_pos++])); break;                   // LONG1
                        case 0x8B: Push(ReadLong(ReadInt32())); break;                    // LONG4
                        case (byte)'G': Push(ReadFloatBE()); break;                       // BINFLOAT
                        case (byte)'s': SetItem(); break;                                // SETITEM
                        case (byte)'u': SetItems(); break;                               // SETITEMS
                        case (byte)'a': Append(); break;                                 // APPEND
                        case (byte)'e': Appends(); break;                                // APPENDS
                        case 0x90: Appends(); break;                                     // ADDITEMS (set)
                        case 0x85: PushTuple(1); break;                                   // TUPLE1
                        case 0x86: PushTuple(2); break;                                   // TUPLE2
                        case 0x87: PushTuple(3); break;                                   // TUPLE3
                        case (byte)'t': PushTupleToMark(); break;                         // TUPLE
                        case (byte)'l': BuildToMark(list: true); break;                   // LIST
                        case (byte)'d': BuildToMark(list: false); break;                  // DICT
                        case 0x93: StackGlobal(); break;                                  // STACK_GLOBAL
                        case (byte)'R': Reduce(); break;                                  // REDUCE
                        default:
                            throw new PickleFormatException(
                                $"Unsupported or executable pickle opcode 0x{op:X2} at offset {_pos - 1}.");
                    }
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException
                                          or InvalidCastException or OverflowException)
            {
                throw new PickleFormatException("Malformed pickle stream.");
            }
        }

        private void Push(object? o) => _stack.Add(o);
        private object? Pop() { var v = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); return v; }
        private int PopMark() { int m = _marks[^1]; _marks.RemoveAt(_marks.Count - 1); return m; }

        private int ReadInt32() { int v = BitConverter.ToInt32(data, _pos); _pos += 4; return v; }
        private ulong ReadUInt64() { ulong v = BitConverter.ToUInt64(data, _pos); _pos += 8; return v; }
        private string ReadStr(int len) { var s = Encoding.UTF8.GetString(data, _pos, len); _pos += len; return s; }
        private byte[] ReadBytes(int len) { var b = new byte[len]; Array.Copy(data, _pos, b, 0, len); _pos += len; return b; }

        private double ReadFloatBE()
        {
            Span<byte> s = stackalloc byte[8];
            for (int i = 0; i < 8; i++) s[i] = data[_pos + 7 - i];
            _pos += 8;
            return BitConverter.ToDouble(s);
        }

        private long ReadLong(int len)
        {
            if (len == 0) return 0;
            long v = 0;
            for (int i = 0; i < len; i++) v |= (long)data[_pos + i] << (8 * i);
            if (len < 8 && (data[_pos + len - 1] & 0x80) != 0) v -= 1L << (8 * len); // sign-extend
            _pos += len;
            return v;
        }

        private void SetItem()
        {
            var v = Pop();
            var k = Pop();
            ((OrderedDictionary<object, object?>)_stack[^1]!)[k!] = v;
        }

        private void SetItems()
        {
            int mark = PopMark();
            var dict = (OrderedDictionary<object, object?>)_stack[mark - 1]!;
            for (int i = mark; i + 1 < _stack.Count; i += 2)
                dict[_stack[i]!] = _stack[i + 1];
            _stack.RemoveRange(mark, _stack.Count - mark);
        }

        private void Append() { var v = Pop(); ((List<object?>)_stack[^1]!).Add(v); }

        private void Appends()
        {
            int mark = PopMark();
            var list = (List<object?>)_stack[mark - 1]!;
            for (int i = mark; i < _stack.Count; i++) list.Add(_stack[i]);
            _stack.RemoveRange(mark, _stack.Count - mark);
        }

        private void PushTuple(int n)
        {
            var t = new object?[n];
            for (int i = n - 1; i >= 0; i--) t[i] = Pop();
            Push(t);
        }

        private void PushTupleToMark()
        {
            int mark = PopMark();
            var t = _stack.GetRange(mark, _stack.Count - mark).ToArray();
            _stack.RemoveRange(mark, _stack.Count - mark);
            Push(t);
        }

        private void BuildToMark(bool list)
        {
            int mark = PopMark();
            if (list)
            {
                var l = new List<object?>(_stack.GetRange(mark, _stack.Count - mark));
                _stack.RemoveRange(mark, _stack.Count - mark);
                Push(l);
            }
            else
            {
                var d = new OrderedDictionary<object, object?>();
                for (int i = mark; i + 1 < _stack.Count; i += 2) d[_stack[i]!] = _stack[i + 1];
                _stack.RemoveRange(mark, _stack.Count - mark);
                Push(d);
            }
        }

        private void StackGlobal()
        {
            var name = Pop() as string;
            var module = Pop() as string;
            Push(new GlobalRef(module ?? "", name ?? ""));
        }

        private void Reduce()
        {
            var arg = Pop();
            var func = Pop();
            if (func is GlobalRef { Module: "builtins", Name: "bytearray" })
            {
                Push(arg switch
                {
                    byte[] b => b,
                    object?[] t when t.Length > 0 && t[0] is byte[] tb => tb,
                    object?[] t when t.Length > 0 && t[0] is string s => Encoding.Latin1.GetBytes(s),
                    _ => Array.Empty<byte>(),
                });
                return;
            }

            throw new PickleFormatException(
                $"REDUCE on unsupported global '{(func as GlobalRef)?.ToString() ?? func?.GetType().Name}'.");
        }

        private sealed record GlobalRef(string Module, string Name)
        {
            public override string ToString() => $"{Module}.{Name}";
        }
    }
}
