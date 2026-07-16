using System.IO.Compression;
using CoffinTranslate.Core.Game;
using CoffinTranslate.Core.IO;
using CoffinTranslate.Core.Packages;

namespace CoffinTranslate.Core.Install;

public enum InstallErrorCode
{
    GameFolderMissing,

    /// <summary>Install name is empty or would collide with the official tool folder.</summary>
    ReservedName,

    /// <summary>An archive entry tried to escape the target folder (zip slip).</summary>
    ArchiveEntryOutsideTarget,
}

public sealed class InstallException(InstallErrorCode code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public InstallErrorCode Code { get; } = code;
}

public sealed record InstallResult(string InstalledPath, bool ReplacedExisting, string? BackupPath);

/// <summary>
/// Installs translation packages into the game's www/languages folder and removes them
/// again. Anything that gets overwritten or removed is moved to the backup folder first
/// (outside the game, so the game never lists backups as languages).
/// </summary>
public sealed class TranslationInstaller(string? backupRootPath = null)
{
    public static string DefaultBackupRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoffinTranslate",
        "backups");

    public string BackupRootPath { get; } = backupRootPath ?? DefaultBackupRoot;

    public InstallResult Install(GameInstallation game, TranslationPackage package, bool backupExisting = true)
    {
        if (!GameLocator.LooksLikeGameFolder(game.RootPath))
            throw new InstallException(InstallErrorCode.GameFolderMissing, $"Game folder is missing or invalid: {game.RootPath}");

        var targetName = NameUtils.SanitizeFileName(package.InstallName);
        if (targetName.Length == 0 || targetName.Equals("tool", StringComparison.OrdinalIgnoreCase))
            throw new InstallException(InstallErrorCode.ReservedName, $"'{package.InstallName}' is not a valid translation name.");

        Directory.CreateDirectory(game.LanguagesPath);
        var targetPath = Path.Combine(game.LanguagesPath, targetName);

        bool existed = File.Exists(targetPath) || Directory.Exists(targetPath);
        string? backupPath = null;
        if (existed)
        {
            if (backupExisting)
                backupPath = MoveToBackup(targetPath, targetName);
            else
                DeleteEntry(targetPath);
        }

        switch (package.Kind)
        {
            case PackageSourceKind.Folder:
                DirectoryUtils.CopyDirectory(package.ContentRootPath, targetPath);
                break;
            case PackageSourceKind.Archive:
                ExtractArchive(package, targetPath);
                break;
            case PackageSourceKind.CldFile:
                File.Copy(package.ContentRootPath, targetPath);
                break;
        }

        return new InstallResult(targetPath, existed, backupPath);
    }

    /// <summary>Removes an installed translation. Returns the backup path if a backup was made.</summary>
    public string? Uninstall(InstalledTranslation translation, bool backup = true)
    {
        if (translation.Name.Equals("tool", StringComparison.OrdinalIgnoreCase))
            throw new InstallException(InstallErrorCode.ReservedName, "The official tool folder cannot be removed.");

        if (!File.Exists(translation.FullPath) && !Directory.Exists(translation.FullPath))
            return null;

        if (backup)
            return MoveToBackup(translation.FullPath, translation.Name);

        DeleteEntry(translation.FullPath);
        return null;
    }

    private static void ExtractArchive(TranslationPackage package, string targetPath)
    {
        using var archive = ZipFile.OpenRead(package.ContentRootPath);

        if (package.Format == DialogueFormat.Cld)
        {
            // ArchiveRootPrefix holds the full entry name of the single .cld file
            var entry = archive.Entries.FirstOrDefault(e =>
                    PackageReader.NormalizeEntryPath(e.FullName)
                        .Equals(package.ArchiveRootPrefix, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Archive entry not found: {package.ArchiveRootPrefix}");
            entry.ExtractToFile(targetPath, overwrite: true);
            return;
        }

        var prefix = package.ArchiveRootPrefix;
        var targetRoot = Path.GetFullPath(targetPath);
        Directory.CreateDirectory(targetRoot);

        foreach (var entry in archive.Entries)
        {
            var entryPath = PackageReader.NormalizeEntryPath(entry.FullName);
            if (!entryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = entryPath[prefix.Length..];
            if (relative.Length == 0)
                continue;

            var destination = Path.GetFullPath(Path.Combine(targetRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(targetRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InstallException(InstallErrorCode.ArchiveEntryOutsideTarget, $"Archive entry escapes the target folder: {entry.FullName}");

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private string MoveToBackup(string sourcePath, string name)
    {
        Directory.CreateDirectory(BackupRootPath);

        var baseName = $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{name}";
        var backupPath = Path.Combine(BackupRootPath, baseName);
        for (int i = 2; File.Exists(backupPath) || Directory.Exists(backupPath); i++)
            backupPath = Path.Combine(BackupRootPath, $"{baseName}_{i}");

        if (Directory.Exists(sourcePath))
            DirectoryUtils.MoveDirectory(sourcePath, backupPath);
        else
            DirectoryUtils.MoveFile(sourcePath, backupPath);

        return backupPath;
    }

    private static void DeleteEntry(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        else if (File.Exists(path))
            File.Delete(path);
    }
}
