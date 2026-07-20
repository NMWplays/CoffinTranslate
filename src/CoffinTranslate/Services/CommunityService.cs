using System.Net.Http;
using CoffinTranslate.Core.Community;

namespace CoffinTranslate.Services;

/// <summary>
/// Fetches the hosted community catalog and downloads packs. The catalog URL is fixed to the
/// project's GitHub repo (not user-configurable, by design). Downloads land in a temp folder and are
/// handed to the normal package installer, so a community install and a manual install share one path.
/// </summary>
public sealed class CommunityService
{
    /// <summary>Raw URL of the catalog in the project repo's root on the default branch.</summary>
    public const string CatalogUrl =
        "https://raw.githubusercontent.com/NMWplays/CoffinTranslate/main/catalog.json";

    private static readonly HttpClient Http = CreateClient();

    private readonly string _downloadDir =
        Path.Combine(Path.GetTempPath(), "CoffinTranslate", "downloads");

    public InstalledPackLedger Ledger { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoffinTranslate", "installed-packs.json"));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CoffinTranslate");
        return client;
    }

    public async Task<CommunityCatalog> FetchCatalogAsync(CancellationToken ct = default)
    {
        var json = await Http.GetStringAsync(CatalogUrl, ct);
        return CommunityCatalogParser.Parse(json);
    }

    /// <summary>
    /// Downloads a pack's ZIP to the temp download folder and returns the local path. Reports 0..1
    /// progress when the server sends a content length.
    /// </summary>
    public async Task<string> DownloadPackAsync(
        CommunityPack pack, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var url = CommunityCatalog.ResolveFileUrl(CatalogUrl, pack.File);
        Directory.CreateDirectory(_downloadDir);
        var dest = Path.Combine(_downloadDir, SanitizeId(pack.Id) + ".zip");

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(dest);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total is > 0 && progress is not null)
                progress.Report(Math.Clamp((double)readTotal / total.Value, 0, 1));
        }

        return dest;
    }

    private static string SanitizeId(string id) =>
        new(id.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray());
}
