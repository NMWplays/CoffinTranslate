using System.ComponentModel;
using System.Text.Json;
using Avalonia.Platform;

namespace CoffinTranslate.Services;

public sealed record UiLanguage(string Code, string DisplayName);

/// <summary>A single localized string that raises PropertyChanged on <see cref="Value"/> whenever
/// the UI language changes, so bindings refresh live. Created and cached by <see cref="Localizer"/>.</summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));
    private readonly Localizer _owner;
    private readonly string _key;

    internal LocalizedString(Localizer owner, string key)
    {
        _owner = owner;
        _key = key;
    }

    public string Value => _owner[_key];

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void RaiseChanged() => PropertyChanged?.Invoke(this, ValueChanged);
}

/// <summary>
/// Runtime-switchable UI localization backed by JSON files in Assets/i18n.
/// Bind via the <see cref="LocalizeExtension"/> markup extension: {loc:Localize Key}.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    public static IReadOnlyList<UiLanguage> SupportedLanguages { get; } =
    [
        new("en", "English"),
        new("de", "Deutsch"),
    ];

    private readonly Dictionary<string, string> _fallback;
    private readonly Dictionary<string, LocalizedString> _notifiers = new();
    private Dictionary<string, string> _strings;

    private Localizer()
    {
        _fallback = LoadLanguageFile("en");
        _strings = _fallback;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage { get; private set; } = "en";

    public string this[string key] =>
        _strings.TryGetValue(key, out var value) ? value
        : _fallback.TryGetValue(key, out var fallback) ? fallback
        : key;

    public string Format(string key, params object?[] args) => string.Format(this[key], args);

    /// <summary>
    /// Returns a per-key notifier whose <see cref="LocalizedString.Value"/> raises PropertyChanged
    /// when the language changes. Binding to this (instead of the indexer directly) makes localized
    /// text update live, without a restart. One shared instance per key.
    /// </summary>
    public LocalizedString Localized(string key)
    {
        if (!_notifiers.TryGetValue(key, out var s))
            _notifiers[key] = s = new LocalizedString(this, key);
        return s;
    }

    public void SetLanguage(string code)
    {
        if (code == CurrentLanguage)
            return;

        _strings = code == "en" ? _fallback : LoadLanguageFile(code);
        CurrentLanguage = code;

        foreach (var notifier in _notifiers.Values)
            notifier.RaiseChanged();

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    /// <summary>The language to use when no preference is stored yet.</summary>
    public static string DetectSystemLanguage()
    {
        var twoLetter = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return SupportedLanguages.Any(l => l.Code == twoLetter) ? twoLetter : "en";
    }

    private static Dictionary<string, string> LoadLanguageFile(string code)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://CoffinTranslate/Assets/i18n/{code}.json"));
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        }
        catch (Exception ex) when (ex is FileNotFoundException or JsonException)
        {
            return [];
        }
    }
}
