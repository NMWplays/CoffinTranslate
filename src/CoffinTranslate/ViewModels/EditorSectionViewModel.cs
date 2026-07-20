using CommunityToolkit.Mvvm.ComponentModel;

namespace CoffinTranslate.ViewModels;

/// <summary>One section in the editor's left list (Labels, Speakers, a dialogue file, …). Owns its
/// cells and builds the row view models lazily when the section is first shown.</summary>
public sealed class EditorSectionViewModel : ObservableObject
{
    private readonly IReadOnlyList<EditorCell> _cells;
    private readonly Action _onProgressChanged;
    private IReadOnlyList<EditorUnitViewModel>? _units;

    public EditorSectionViewModel(string title, IReadOnlyList<EditorCell> cells, Action onProgressChanged)
    {
        Title = title;
        _cells = cells;
        _onProgressChanged = onProgressChanged;
    }

    public string Title { get; }

    public int Total => _cells.Count;

    public int Translated => _cells.Count(c => c.IsTranslated());

    public string ProgressText => $"{Translated}/{Total}";

    /// <summary>Completion as a 0–1 fraction, for the statistics progress bars.</summary>
    public double Fraction => Total == 0 ? 0 : (double)Translated / Total;

    public IReadOnlyList<EditorUnitViewModel> Units =>
        _units ??= _cells.Select(c => new EditorUnitViewModel(c, OnUnitChanged)).ToList();

    /// <summary>True if any cell's header (ID), source, or translation contains the search term.
    /// Works off the lightweight cells, so it doesn't force the row view models to be built.</summary>
    public bool MatchesSearch(string search)
    {
        foreach (var c in _cells)
            if (c.Header.Contains(search, StringComparison.OrdinalIgnoreCase)
                || c.Source.Contains(search, StringComparison.OrdinalIgnoreCase)
                || c.ReadTarget().Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void OnUnitChanged()
    {
        RefreshProgress();
        _onProgressChanged();
    }

    public void RefreshProgress()
    {
        OnPropertyChanged(nameof(Translated));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(Fraction));
    }
}
