namespace CoffinTranslate.Core.IO;

public static class NameUtils
{
    /// <summary>
    /// Makes a string safe to use as a single file/folder name: strips path separators
    /// and invalid characters, trims trailing dots/spaces, rejects traversal.
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim()
            .Select(c => invalid.Contains(c) || c is '/' or '\\' ? '_' : c)
            .ToArray();

        var sanitized = new string(chars).TrimEnd('.', ' ');
        return sanitized is "." or ".." ? "" : sanitized;
    }
}
