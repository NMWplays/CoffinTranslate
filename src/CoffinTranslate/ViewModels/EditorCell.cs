namespace CoffinTranslate.ViewModels;

/// <summary>
/// A view-agnostic handle to one translatable unit: how to display it, its source text, and how
/// to read and write its target. Decouples the editor view models from the two underlying unit
/// shapes (scalar vs. multi-line) so both edit the same way.
/// </summary>
public sealed record EditorCell(
    string Header,
    string Source,
    Func<string> ReadTarget,
    Action<string> WriteTarget,
    Func<bool> IsTranslated,
    string Speaker = "",
    Func<bool>? IsNew = null,
    Action? ClearNew = null);
