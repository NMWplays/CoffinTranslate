namespace CoffinTranslate.Core.Project;

/// <summary>
/// Writes a project's replacement images into an export folder, mirroring the game's own image
/// layout. A source key <c>img/pictures/&lt;hash&gt;</c> becomes <c>&lt;target&gt;/pictures/&lt;hash&gt;</c>
/// (bare name, no extension) — exactly how the game finds a translated image.
/// </summary>
public static class TranslationImageExporter
{
    /// <summary>
    /// Copies every replacement whose source file still exists into <paramref name="targetDir"/>.
    /// Returns the keys whose chosen file was missing, so the caller can warn about them.
    /// </summary>
    public static IReadOnlyList<string> Export(TranslationProject project, string targetDir)
    {
        var missing = new List<string>();
        var targetRoot = Path.GetFullPath(targetDir);

        foreach (var (key, sourcePath) in project.ImageReplacements)
        {
            if (RelativePath(key) is not { } relative)
                continue;

            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                missing.Add(key);
                continue;
            }

            var destination = Path.GetFullPath(
                Path.Combine(targetRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(targetRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue; // defends against a malformed key escaping the target folder

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourcePath, destination, overwrite: true);
        }

        return missing;
    }

    /// <summary>
    /// The translation-relative path for an image key: <c>img/pictures/&lt;hash&gt;</c> →
    /// <c>pictures/&lt;hash&gt;</c>. Returns <see langword="null"/> for keys that don't start with
    /// <c>img/</c> or that contain path traversal.
    /// </summary>
    public static string? RelativePath(string key)
    {
        const string prefix = "img/";
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var relative = key[prefix.Length..];
        if (relative.Length == 0 || relative.Contains("..") || Path.IsPathRooted(relative))
            return null;

        return relative;
    }
}
