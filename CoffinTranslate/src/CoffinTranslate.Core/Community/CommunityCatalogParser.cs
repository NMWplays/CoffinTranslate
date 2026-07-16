using System.Text.Json;

namespace CoffinTranslate.Core.Community;

/// <summary>Thrown when the community catalog JSON is malformed and cannot be parsed at all.</summary>
public sealed class CommunityCatalogException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Parses the hosted <c>catalog.json</c> into a <see cref="CommunityCatalog"/>. Lenient by design:
/// unknown fields are ignored and a single malformed pack entry is skipped rather than failing the
/// whole catalog, so one typo can't hide every other translation. Only invalid top-level JSON throws.
/// </summary>
public static class CommunityCatalogParser
{
    public static CommunityCatalog Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new CommunityCatalogException("Catalog is not valid JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new CommunityCatalogException("Catalog root is not a JSON object.");

            int schema = root.TryGetProperty("schema", out var s) && s.ValueKind == JsonValueKind.Number
                ? s.GetInt32()
                : 1;

            var packs = new List<CommunityPack>();
            if (root.TryGetProperty("packs", out var packsElement) && packsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in packsElement.EnumerateArray())
                {
                    if (TryReadPack(entry) is { } pack)
                        packs.Add(pack);
                }
            }

            return new CommunityCatalog(schema, packs);
        }
    }

    private static CommunityPack? TryReadPack(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        var id = Str(entry, "id");
        var language = Str(entry, "language");
        var version = Str(entry, "version");
        var file = Str(entry, "file");

        // required fields — skip an entry that can't be identified, listed, versioned or downloaded
        if (id is null || language is null || version is null || file is null)
            return null;

        return new CommunityPack
        {
            Id = id,
            Language = language,
            Version = version,
            File = file,
            Authors = Str(entry, "authors") ?? "",
            GameVersion = Str(entry, "gameVersion"),
            Description = Str(entry, "description"),
            Source = Str(entry, "source"),
        };
    }

    /// <summary>Reads a non-empty string property, or null if absent/blank/not a string.</summary>
    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}
