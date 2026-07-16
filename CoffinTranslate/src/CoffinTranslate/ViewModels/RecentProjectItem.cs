namespace CoffinTranslate.ViewModels;

/// <summary>One entry in the editor's "recently used projects" list.</summary>
public sealed record RecentProjectItem(string Path)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? Path;
}
