namespace CoffinTranslate.Core.Community;

/// <summary>
/// A community translation the app can download and install with one click. Mirrors one entry of
/// the hosted <c>catalog.json</c>. Only <see cref="Id"/>, <see cref="Language"/>, <see cref="Version"/>
/// and <see cref="File"/> are required; the rest is display metadata.
/// </summary>
public sealed record CommunityPack
{
    /// <summary>Stable identifier of this pack across catalog updates (e.g. <c>"deutsch-marie"</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Display language name (e.g. <c>"Deutsch"</c>).</summary>
    public required string Language { get; init; }

    /// <summary>Author/translator credit line.</summary>
    public string Authors { get; init; } = "";

    /// <summary>Pack version, used to offer updates (e.g. <c>"1.3"</c>).</summary>
    public required string Version { get; init; }

    /// <summary>The game version this pack targets (e.g. <c>"3.0.13"</c>), for a compatibility hint.</summary>
    public string? GameVersion { get; init; }

    public string? Description { get; init; }

    /// <summary>Link to the original release thread (attribution).</summary>
    public string? Source { get; init; }

    /// <summary>
    /// The ZIP to download, either an absolute URL or a path relative to the catalog URL
    /// (e.g. <c>"packs/deutsch.zip"</c>). Resolve with <see cref="CommunityCatalog.ResolveFileUrl"/>.
    /// </summary>
    public required string File { get; init; }
}

/// <summary>The parsed <c>catalog.json</c>: a schema version and the list of available packs.</summary>
public sealed record CommunityCatalog(int Schema, IReadOnlyList<CommunityPack> Packs)
{
    /// <summary>
    /// Resolves a pack's <see cref="CommunityPack.File"/> against the catalog's own URL, so a
    /// relative <c>packs/deutsch.zip</c> becomes an absolute download URL. Absolute URLs pass through.
    /// </summary>
    public static string ResolveFileUrl(string catalogUrl, string file)
    {
        if (Uri.TryCreate(file, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        return new Uri(new Uri(catalogUrl), file).ToString();
    }
}
