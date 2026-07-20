using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CoffinTranslate.ViewModels;

namespace CoffinTranslate.Views;

public partial class InstallView : UserControl
{
    public InstallView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        UpdateEffects(e);
        if (e.DragEffects != DragDropEffects.None)
            DropZone.Classes.Add("dragover");
    }

    private void OnDragOver(object? sender, DragEventArgs e) => UpdateEffects(e);

    private void OnDragLeave(object? sender, DragEventArgs e) => DropZone.Classes.Remove("dragover");

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Remove("dragover");
        e.Handled = true;

        var path = e.DataTransfer.TryGetFiles()?
            .Select(f => f.TryGetLocalPath())
            .FirstOrDefault(p => p is not null);

        if (path is not null && DataContext is InstallViewModel viewModel)
            await viewModel.LoadPackageAsync(path);
    }

    private static void UpdateEffects(DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }
}
