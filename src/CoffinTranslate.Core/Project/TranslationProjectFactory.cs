using CoffinTranslate.Core.SourceData;

namespace CoffinTranslate.Core.Project;

/// <summary>Builds an editable <see cref="TranslationProject"/> from the game's source catalog.</summary>
public static class TranslationProjectFactory
{
    /// <summary>
    /// The pseudo-speaker the game's data uses for "Show Choices" menu options. A section entry whose
    /// speaker equals this is a choice block, not narration/dialogue (see docs/official-tool-analysis.md).
    /// </summary>
    public const string ChoiceSpeaker = "[CHOICES]";

    /// <summary>The label the official TXT format prints for entries with no speaker.</summary>
    public const string NarratorName = "Narrator";

    /// <summary>
    /// Creates a fresh, untranslated project: every source string is present with an empty target,
    /// ready for the editor. Targets left empty fall back to English in-game.
    /// </summary>
    public static TranslationProject FromCatalog(GameSourceCatalog catalog)
    {
        var project = new TranslationProject
        {
            LanguageName = "",
            FontFace = catalog.FontFace ?? "GameFont",
            FontSize = catalog.FontSize ?? 28,
            Credits = ["", "", ""],
            SourceVersionHash = catalog.VersionHash,
        };

        foreach (var (key, source) in catalog.SystemLabels)
            project.Labels.Add(new ScalarUnit { Key = key, Source = source });

        foreach (var (key, source) in catalog.Menus)
            project.Menus.Add(new ScalarUnit { Key = key, Source = source });

        foreach (var (id, name) in catalog.Speakers)
            project.Speakers.Add(new ScalarUnit { Key = id, Source = name });

        foreach (var (id, name) in catalog.Items)
        {
            project.Items.Add(new ScalarUnit { Key = id, Source = name });
            project.Descriptions.Add(new TextUnit
            {
                Id = id,
                Annotation = name,
                Source = catalog.Texts.TryGetValue(id, out var lines) ? lines : [""],
                Target = [],
            });
        }

        foreach (var section in catalog.Sections)
        {
            var file = new DialogueFile { FileName = section.FileName };
            foreach (var entry in section.Entries)
            {
                if (entry.SpeakerId == ChoiceSpeaker)
                {
                    // one section entry = one on-screen menu; each text id is one option line
                    bool first = true;
                    foreach (var textId in entry.TextIds)
                    {
                        file.Entries.Add(new TextUnit
                        {
                            Id = textId,
                            Annotation = "",
                            Source = catalog.Texts.TryGetValue(textId, out var lines) ? lines : [""],
                            Target = [],
                            IsChoice = true,
                            ChoiceGroupStart = first,
                        });
                        first = false;
                    }
                    continue;
                }

                var speaker = entry.SpeakerId.Length == 0
                    ? NarratorName
                    : catalog.Speakers.TryGetValue(entry.SpeakerId, out var n) ? n : "";
                foreach (var textId in entry.TextIds)
                {
                    file.Entries.Add(new TextUnit
                    {
                        Id = textId,
                        Annotation = speaker,
                        Source = catalog.Texts.TryGetValue(textId, out var lines) ? lines : [""],
                        Target = [],
                    });
                }
            }

            project.Dialogue.Add(file);
        }

        return project;
    }

    /// <summary>
    /// Fills the <c>Source</c> of a project read from a <c>dialogue.txt</c> (which stores only
    /// translations) by matching each unit's key/ID against the game's source catalog. Units with
    /// no match keep an empty source.
    /// </summary>
    public static void MergeSource(TranslationProject project, GameSourceCatalog catalog)
    {
        project.SourceVersionHash ??= catalog.VersionHash;

        foreach (var unit in project.Labels)
            if (catalog.SystemLabels.TryGetValue(unit.Key, out var s)) unit.Source = s;
        foreach (var unit in project.Menus)
            if (catalog.Menus.TryGetValue(unit.Key, out var s)) unit.Source = s;
        foreach (var unit in project.Speakers)
            if (catalog.Speakers.TryGetValue(unit.Key, out var s)) unit.Source = s;
        foreach (var unit in project.Items)
            if (catalog.Items.TryGetValue(unit.Key, out var s)) unit.Source = s;

        foreach (var unit in project.Descriptions)
        {
            if (catalog.Texts.TryGetValue(unit.Id, out var lines)) unit.Source = lines;
            if (unit.Annotation.Length == 0 && catalog.Items.TryGetValue(unit.Id, out var name))
                unit.Annotation = name;
        }

        foreach (var file in project.Dialogue)
            foreach (var unit in file.Entries)
                if (catalog.Texts.TryGetValue(unit.Id, out var lines)) unit.Source = lines;
    }

    /// <summary>
    /// Reconciles an existing project against a newer game catalog: adds strings the game gained,
    /// refreshes the source of strings whose English text changed, and flags both (<c>IsNew</c>)
    /// so the translator can find what needs (re)doing. Nothing is removed — strings the game
    /// dropped are only counted. Existing translations are always kept.
    /// </summary>
    public static UpdateResult UpdateFromCatalog(TranslationProject project, GameSourceCatalog catalog)
    {
        int added = 0, changed = 0, removed = 0;

        void MergeScalars(List<ScalarUnit> list, IReadOnlyDictionary<string, string> source)
        {
            var byKey = new Dictionary<string, ScalarUnit>(StringComparer.Ordinal);
            foreach (var u in list)
                byKey[u.Key] = u;

            foreach (var (key, text) in source)
            {
                if (byKey.TryGetValue(key, out var unit))
                {
                    if (!string.Equals(unit.Source, text, StringComparison.Ordinal))
                    {
                        unit.Source = text;
                        unit.IsNew = true;
                        changed++;
                    }
                }
                else
                {
                    list.Add(new ScalarUnit { Key = key, Source = text, IsNew = true });
                    added++;
                }
            }

            foreach (var u in list)
                if (!source.ContainsKey(u.Key))
                    removed++;
        }

        MergeScalars(project.Labels, catalog.SystemLabels);
        MergeScalars(project.Menus, catalog.Menus);
        MergeScalars(project.Speakers, catalog.Speakers);
        MergeScalars(project.Items, catalog.Items);

        // item descriptions (keyed by item id, sourced from the universal text store)
        var descById = new Dictionary<string, TextUnit>(StringComparer.Ordinal);
        foreach (var u in project.Descriptions)
            descById[u.Id] = u;
        foreach (var (id, name) in catalog.Items)
        {
            var source = SourceFor(catalog, id);
            if (descById.TryGetValue(id, out var unit))
            {
                if (!SourceEquals(unit.Source, source))
                {
                    unit.Source = source;
                    unit.IsNew = true;
                    changed++;
                }
                if (unit.Annotation.Length == 0)
                    unit.Annotation = name;
            }
            else
            {
                project.Descriptions.Add(new TextUnit { Id = id, Annotation = name, Source = source, Target = [], IsNew = true });
                added++;
            }
        }
        foreach (var u in project.Descriptions)
            if (!catalog.Items.ContainsKey(u.Id))
                removed++;

        // dialogue: reconcile every catalog section into its file, appending genuinely new ids
        var dialogueById = new Dictionary<string, TextUnit>(StringComparer.Ordinal);
        var filesByName = new Dictionary<string, DialogueFile>(StringComparer.Ordinal);
        foreach (var file in project.Dialogue)
        {
            filesByName[file.FileName] = file;
            foreach (var u in file.Entries)
                dialogueById[u.Id] = u;
        }

        var catalogTextIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in catalog.Sections)
        {
            if (!filesByName.TryGetValue(section.FileName, out var file))
            {
                file = new DialogueFile { FileName = section.FileName };
                project.Dialogue.Add(file);
                filesByName[section.FileName] = file;
            }

            foreach (var entry in section.Entries)
            {
                var isChoice = entry.SpeakerId == ChoiceSpeaker;
                var speaker = isChoice
                    ? ""
                    : entry.SpeakerId.Length == 0
                        ? NarratorName
                        : catalog.Speakers.TryGetValue(entry.SpeakerId, out var n) ? n : "";

                var first = true;
                foreach (var textId in entry.TextIds)
                {
                    catalogTextIds.Add(textId);
                    var source = SourceFor(catalog, textId);
                    if (dialogueById.TryGetValue(textId, out var unit))
                    {
                        if (!SourceEquals(unit.Source, source))
                        {
                            unit.Source = source;
                            unit.IsNew = true;
                            changed++;
                        }
                    }
                    else
                    {
                        var added2 = new TextUnit
                        {
                            Id = textId,
                            Annotation = speaker,
                            Source = source,
                            Target = [],
                            IsChoice = isChoice,
                            ChoiceGroupStart = isChoice && first,
                            IsNew = true,
                        };
                        file.Entries.Add(added2);
                        dialogueById[textId] = added2;
                        added++;
                    }
                    first = false;
                }
            }
        }
        foreach (var id in dialogueById.Keys)
            if (!catalogTextIds.Contains(id))
                removed++;

        project.SourceVersionHash = catalog.VersionHash;
        return new UpdateResult(added, changed, removed);
    }

    private static IReadOnlyList<string> SourceFor(GameSourceCatalog catalog, string id) =>
        catalog.Texts.TryGetValue(id, out var lines) ? lines : [""];

    private static bool SourceEquals(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                return false;
        return true;
    }
}

/// <summary>Outcome of an <see cref="TranslationProjectFactory.UpdateFromCatalog"/> run.</summary>
/// <param name="Added">Strings the game gained (added to the project, flagged new).</param>
/// <param name="Changed">Existing strings whose English source changed (refreshed, flagged new).</param>
/// <param name="Removed">Strings the game dropped (kept in the project, only counted).</param>
public readonly record struct UpdateResult(int Added, int Changed, int Removed)
{
    public bool HasChanges => Added > 0 || Changed > 0 || Removed > 0;
}
