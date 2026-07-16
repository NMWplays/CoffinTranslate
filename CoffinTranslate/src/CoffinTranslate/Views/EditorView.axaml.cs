using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CoffinTranslate.ViewModels;

namespace CoffinTranslate.Views;

public partial class EditorView : UserControl
{
    private EditorViewModel? _vm;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.ScrollToUnitRequested -= ScrollToUnit;
        _vm = DataContext as EditorViewModel;
        if (_vm is not null)
            _vm.ScrollToUnitRequested += ScrollToUnit;
    }

    // Jump-to-last-edit: scroll the row into view once the section switch / filter rebuild has
    // realized the list's containers (hence the low-priority post).
    private void ScrollToUnit(EditorUnitViewModel unit) =>
        Dispatcher.UIThread.Post(() => UnitsList?.ScrollIntoView(unit), DispatcherPriority.Background);

    // Ctrl+F focuses the search box (only meaningful in the text view). Ctrl+S is a KeyBinding.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && SearchBox is { IsVisible: true } box)
        {
            box.Focus();
            box.SelectAll();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
