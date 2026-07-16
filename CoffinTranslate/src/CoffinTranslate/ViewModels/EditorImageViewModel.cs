using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoffinTranslate.ViewModels;

/// <summary>One translatable game image: its original (shown for reference) and the user's optional
/// replacement PNG. The replacement is remembered as a file path on the project and copied into the
/// export's folder structure at build time.</summary>
public partial class EditorImageViewModel : ObservableObject
{
    public EditorImageViewModel(string key, Bitmap? originalPreview, byte[]? originalBytes, string? replacementPath)
    {
        Key = key;
        OriginalPreview = originalPreview;
        OriginalBytes = originalBytes;

        var parts = key.Split('/');
        FolderLabel = parts.Length >= 2 ? parts[1] : key;
        ShortName = parts.Length >= 1 ? parts[^1] : key;

        SetReplacement(replacementPath);
    }

    /// <summary>Source key, e.g. <c>img/pictures/&lt;hash&gt;</c>.</summary>
    public string Key { get; }

    /// <summary>The original image's raw, decoded PNG bytes — used to open it in an external viewer.</summary>
    public byte[]? OriginalBytes { get; }

    /// <summary>Folder the image lives in (pictures, system, titles1, parallaxes).</summary>
    public string FolderLabel { get; }

    /// <summary>The bare hash filename, shown as a compact identifier.</summary>
    public string ShortName { get; }

    public Bitmap? OriginalPreview { get; }

    public string? ReplacementPath { get; private set; }

    [ObservableProperty]
    public partial Bitmap? ReplacementPreview { get; set; }

    [ObservableProperty]
    public partial bool HasReplacement { get; set; }

    /// <summary>Set when the chosen file path no longer resolves to a readable image.</summary>
    [ObservableProperty]
    public partial bool ReplacementMissing { get; set; }

    public void SetReplacement(string? path)
    {
        ReplacementPath = path;
        ReplacementPreview = null;
        ReplacementMissing = false;
        HasReplacement = !string.IsNullOrEmpty(path);

        if (!HasReplacement)
            return;

        try
        {
            using var stream = File.OpenRead(path!);
            ReplacementPreview = Bitmap.DecodeToWidth(stream, 260);
        }
        catch
        {
            ReplacementMissing = true;
        }
    }
}
