namespace CoffinTranslate.Core.IO;

public static class DirectoryUtils
{
    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var source = new DirectoryInfo(sourceDir);
        if (!source.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        foreach (var file in source.EnumerateFiles())
            file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite: true);

        foreach (var subDir in source.EnumerateDirectories())
            CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name));
    }

    /// <summary>Moves a directory, falling back to copy+delete for cross-volume moves.</summary>
    public static void MoveDirectory(string sourceDir, string destinationDir)
    {
        try
        {
            Directory.Move(sourceDir, destinationDir);
        }
        catch (IOException)
        {
            CopyDirectory(sourceDir, destinationDir);
            Directory.Delete(sourceDir, recursive: true);
        }
    }

    /// <summary>Moves a file, falling back to copy+delete for cross-volume moves.</summary>
    public static void MoveFile(string sourceFile, string destinationFile)
    {
        try
        {
            File.Move(sourceFile, destinationFile);
        }
        catch (IOException)
        {
            File.Copy(sourceFile, destinationFile, overwrite: false);
            File.Delete(sourceFile);
        }
    }
}
