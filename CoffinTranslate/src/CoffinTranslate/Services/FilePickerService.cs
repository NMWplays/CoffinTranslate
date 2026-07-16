using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CoffinTranslate.Services;

public interface IFilePickerService
{
    Task<string?> PickFolderAsync(string title);

    Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType> fileTypes);

    Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FilePickerFileType> fileTypes);
}

/// <summary>File/folder pickers bound to the main window's storage provider.</summary>
public sealed class FilePickerService(Func<TopLevel?> topLevelAccessor) : IFilePickerService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        if (topLevelAccessor() is not { } topLevel)
            return null;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType> fileTypes)
    {
        if (topLevelAccessor() is not { } topLevel)
            return null;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes,
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FilePickerFileType> fileTypes)
    {
        if (topLevelAccessor() is not { } topLevel)
            return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = fileTypes,
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }
}
