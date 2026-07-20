using CoffinTranslate.Core.Install;
using CoffinTranslate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoffinTranslate.ViewModels;

public partial class InstalledItemViewModel(InstalledTranslation model, ManageViewModel owner) : ViewModelBase
{
    private static Localizer L => Localizer.Instance;

    public InstalledTranslation Model { get; } = model;

    [ObservableProperty]
    public partial bool IsConfirmingRemove { get; set; }

    public string DisplayLanguage => Model.DisplayLanguage;

    public string Detail
    {
        get
        {
            if (!Model.IsRecognized)
                return L["Manage_Unrecognized"];

            var kind = Model.Kind == InstalledKind.CldFile ? L["Manage_Kind_Cld"] : L["Manage_Kind_Folder"];
            return Model.Metadata?.Credits is { Count: > 0 } credits
                ? $"{kind} · {L.Format("Manage_By", string.Join(", ", credits))}"
                : kind;
        }
    }

    public string PathText => Model.FullPath;

    public void RefreshTexts() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private void BeginRemove() => IsConfirmingRemove = true;

    [RelayCommand]
    private void CancelRemove() => IsConfirmingRemove = false;

    [RelayCommand]
    private Task ConfirmRemoveAsync() => owner.RemoveAsync(this);

    [RelayCommand]
    private void OpenFolder() =>
        ExplorerService.OpenFolder(Model.Kind == InstalledKind.CldFile
            ? Path.GetDirectoryName(Model.FullPath)!
            : Model.FullPath);
}
