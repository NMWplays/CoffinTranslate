using CoffinTranslate.Core.Project;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoffinTranslate.ViewModels;

/// <summary>One editable row in the editor: read-only source next to an editable translation,
/// with live "translated" and formatting-code-mismatch flags.</summary>
public partial class EditorUnitViewModel : ObservableObject
{
    private readonly EditorCell _cell;
    private readonly Action _onChanged;
    private string _targetText;

    public EditorUnitViewModel(EditorCell cell, Action onChanged)
    {
        _cell = cell;
        _onChanged = onChanged;
        _targetText = cell.ReadTarget();
        Refresh();
    }

    public string Header => _cell.Header;

    public string Source => _cell.Source;

    /// <summary>The speaker/character name for dialogue rows (empty for scalar rows and choices).</summary>
    public string Speaker => _cell.Speaker;

    public string TargetText
    {
        get => _targetText;
        set
        {
            if (!SetProperty(ref _targetText, value))
                return;

            _cell.WriteTarget(value);
            _cell.ClearNew?.Invoke(); // touching a line resolves its "new/changed" flag
            Refresh();
            _onChanged();
        }
    }

    [ObservableProperty]
    public partial bool IsTranslated { get; set; }

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool HasTagWarning { get; set; }

    private void Refresh()
    {
        IsTranslated = _cell.IsTranslated();
        IsNew = _cell.IsNew?.Invoke() ?? false;
        HasTagWarning = !FormattingTags.Consistent(_cell.Source, _targetText);
    }
}
