using System.IO.Compression;
using CoffinTranslate.Core.Serialization;

namespace CoffinTranslate.Core.SourceData;

/// <summary>
/// Reads the game's <c>Translator.dat</c> (a zlib-compressed, data-only Python pickle) into a
/// <see cref="GameSourceCatalog"/>. Reading is for format interoperability only: the file stays
/// in the user's own game folder and no game content is redistributed.
/// </summary>
public static class GameSourceReader
{
    /// <summary>Reads and decodes a <c>Translator.dat</c> file from disk.</summary>
    public static GameSourceCatalog ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads and decodes a <c>Translator.dat</c> stream (zlib → pickle → catalog).</summary>
    public static GameSourceCatalog Read(Stream datStream)
    {
        return FromPickle(Decompress(datStream));
    }

    /// <summary>
    /// Extracts the translatable images from a <c>Translator.dat</c>: source key
    /// (e.g. <c>img/pictures/&lt;hash&gt;</c>) → the raw, decrypted PNG bytes. Loaded on demand
    /// (the payload is large) and only for format interoperability — nothing is redistributed.
    /// </summary>
    public static IReadOnlyDictionary<string, byte[]> ReadImages(string path)
    {
        using var stream = File.OpenRead(path);
        return ReadImages(stream);
    }

    /// <inheritdoc cref="ReadImages(string)"/>
    public static IReadOnlyDictionary<string, byte[]> ReadImages(Stream datStream)
    {
        if (PythonPickle.Load(Decompress(datStream)) is not OrderedDictionary<object, object?> root)
            throw new GameSourceFormatException("Translator.dat root is not a dictionary.");

        var result = new Dictionary<string, byte[]>();
        if (Get(root, "img_data") is OrderedDictionary<object, object?> images)
            foreach (var (key, value) in images)
                if (key is string k && value is byte[] bytes)
                    result[k] = bytes;

        return result;
    }

    private static byte[] Decompress(Stream datStream)
    {
        using var zlib = new ZLibStream(datStream, CompressionMode.Decompress, leaveOpen: true);
        using var mem = new MemoryStream();
        zlib.CopyTo(mem);
        return mem.ToArray();
    }

    /// <summary>Decodes an already-decompressed pickle payload. Exposed for testing.</summary>
    public static GameSourceCatalog FromPickle(byte[] pickle)
    {
        if (PythonPickle.Load(pickle) is not OrderedDictionary<object, object?> root)
            throw new GameSourceFormatException("Translator.dat root is not a dictionary.");

        var versionHash = Str(Get(root, "ver_hash")) ?? "";
        var (version, hash) = SplitVersionHash(versionHash);

        return new GameSourceCatalog
        {
            VersionHash = versionHash,
            Version = version,
            DataHash = hash,
            LanguageName = Str(Get(root, "lng_name")) ?? "English",
            Credits = StrList(Get(root, "lng_info")),
            FontFace = Str(Get(root, "fnt_face")),
            FontSize = Get(root, "fnt_size") is long size ? (int)size : null,
            HasEmbeddedFont = Get(root, "fnt_data") is byte[],
            SystemLabels = StrMap(Get(root, "sys_lbls")),
            Menus = StrMap(Get(root, "sys_menu")),
            Speakers = StrMap(Get(root, "actr_lut")),
            Items = StrMap(Get(root, "item_lut")),
            Texts = TextMap(Get(root, "text_lut")),
            Sections = ReadSections(Get(root, "sections")),
            ImagePaths = KeyList(Get(root, "img_data")),
        };
    }

    private static (string? version, string? hash) SplitVersionHash(string value)
    {
        int sep = value.IndexOf(':');
        return sep < 0
            ? (value.Trim() is { Length: > 0 } v ? v : null, null)
            : (value[..sep].Trim(), value[(sep + 1)..].Trim());
    }

    private static IReadOnlyList<DialogueSection> ReadSections(object? value)
    {
        if (value is not OrderedDictionary<object, object?> files)
            return [];

        var sections = new List<DialogueSection>(files.Count);
        foreach (var (fileKey, entriesValue) in files)
        {
            if (fileKey is not string fileName || entriesValue is not List<object?> rawEntries)
                continue;

            var entries = new List<DialogueEntry>(rawEntries.Count);
            foreach (var raw in rawEntries)
            {
                if (raw is not OrderedDictionary<object, object?> entry)
                    continue;

                entries.Add(new DialogueEntry(
                    Str(Get(entry, "name")) ?? "",
                    StrList(Get(entry, "text"))));
            }

            sections.Add(new DialogueSection(fileName, entries));
        }

        return sections;
    }

    // --- pickle shape helpers ---

    private static object? Get(OrderedDictionary<object, object?> dict, string key) =>
        dict.TryGetValue(key, out var value) ? value : null;

    private static string? Str(object? value) => value as string;

    private static IReadOnlyList<string> StrList(object? value) => value switch
    {
        List<object?> list => list.Select(x => x as string ?? "").ToList(),
        object?[] tuple => tuple.Select(x => x as string ?? "").ToList(),
        _ => [],
    };

    private static IReadOnlyList<string> KeyList(object? value) =>
        value is OrderedDictionary<object, object?> dict
            ? dict.Keys.OfType<string>().ToList()
            : [];

    private static IReadOnlyDictionary<string, string> StrMap(object? value)
    {
        var result = new Dictionary<string, string>();
        if (value is OrderedDictionary<object, object?> dict)
            foreach (var (k, v) in dict)
                if (k is string key)
                    result[key] = v as string ?? "";
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> TextMap(object? value)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>();
        if (value is OrderedDictionary<object, object?> dict)
            foreach (var (k, v) in dict)
                if (k is string key)
                    result[key] = StrList(v);
        return result;
    }
}
