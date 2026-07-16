using System.Text.Json;

namespace CoffinTranslate.Core.Community;

/// <summary>What was installed from the community catalog for a given pack id.</summary>
public sealed record InstalledPackRecord(string InstallName, string Version);

/// <summary>
/// Remembers which community packs (and which versions) the user installed, keyed by pack id. The
/// installed language folder itself carries no version, so this local ledger is what lets the
/// community list show "installed" vs. "update available". Stored as a small JSON file in the app's
/// local data — never inside the game folder. Reads/writes are best-effort: a missing or corrupt
/// ledger just means "nothing recorded yet".
/// </summary>
public sealed class InstalledPackLedger
{
    private readonly string _path;
    private Dictionary<string, InstalledPackRecord> _records;

    public InstalledPackLedger(string path)
    {
        _path = path;
        _records = Load(path);
    }

    public InstalledPackRecord? Get(string packId) =>
        _records.TryGetValue(packId, out var record) ? record : null;

    public void Record(string packId, string installName, string version)
    {
        _records[packId] = new InstalledPackRecord(installName, version);
        Save();
    }

    public void Remove(string packId)
    {
        if (_records.Remove(packId))
            Save();
    }

    private static Dictionary<string, InstalledPackRecord> Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var records = JsonSerializer.Deserialize<Dictionary<string, InstalledPackRecord>>(File.ReadAllText(path));
                if (records is not null)
                    return new Dictionary<string, InstalledPackRecord>(records, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // corrupt or unreadable ledger — start fresh rather than blocking the feature
        }

        return new Dictionary<string, InstalledPackRecord>(StringComparer.Ordinal);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best-effort: failing to persist the ledger must never crash an otherwise-successful install
        }
    }
}
