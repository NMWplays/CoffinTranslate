namespace CoffinTranslate.Core.Community;

/// <summary>How a catalog pack relates to what's currently installed.</summary>
public enum PackInstallState
{
    /// <summary>Not installed from the catalog (or its folder was removed since).</summary>
    NotInstalled,

    /// <summary>Installed, and the catalog offers the same version.</summary>
    Installed,

    /// <summary>Installed, but the catalog has a newer version.</summary>
    UpdateAvailable,
}

public static class CommunityInstall
{
    /// <summary>
    /// Decides a pack's state from the ledger record (what we installed for this pack id) and the
    /// language folders actually present in the game. A record whose folder is gone counts as
    /// not installed, so removing a translation in the Manage page correctly resets the button.
    /// </summary>
    public static PackInstallState DetermineState(
        CommunityPack pack, InstalledPackRecord? record, IEnumerable<string> installedFolderNames)
    {
        if (record is null)
            return PackInstallState.NotInstalled;

        bool present = installedFolderNames.Any(
            n => string.Equals(n, record.InstallName, StringComparison.OrdinalIgnoreCase));
        if (!present)
            return PackInstallState.NotInstalled;

        return CommunityVersion.IsNewer(pack.Version, record.Version)
            ? PackInstallState.UpdateAvailable
            : PackInstallState.Installed;
    }
}
