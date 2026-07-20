using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoffinTranslate.Core.Project;

/// <summary>
/// Reads and writes the editor's native project file (<c>.ctproj</c>, JSON, UTF-8). Unlike the
/// game's <c>dialogue.txt</c> — which fills every untranslated line with English so the export is
/// playable — this format keeps untranslated targets empty, so reopening a project resumes work
/// exactly where it stopped instead of showing English as if it were translated. It also stores the
/// source text, so a project can be reopened without the game present.
/// </summary>
public static class TranslationProjectJson
{
    public const string FileExtension = ".ctproj";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void WriteFile(TranslationProject project, string path) =>
        File.WriteAllText(path, Write(project));

    public static string Write(TranslationProject project) =>
        JsonSerializer.Serialize(ToDto(project), Options);

    public static TranslationProject ReadFile(string path) =>
        Read(File.ReadAllText(path));

    public static TranslationProject Read(string json)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(json, Options)
            ?? throw new JsonException("Project file is empty or invalid.");
        return FromDto(dto);
    }

    private static ProjectDto ToDto(TranslationProject p) => new()
    {
        LanguageName = p.LanguageName,
        FontFace = p.FontFace,
        FontSize = p.FontSize,
        FontFilePath = p.FontFilePath,
        Credits = [.. p.Credits],
        SourceVersionHash = p.SourceVersionHash,
        EditingSeconds = p.EditingSeconds,
        Labels = p.Labels.Select(ToScalar).ToList(),
        Menus = p.Menus.Select(ToScalar).ToList(),
        Speakers = p.Speakers.Select(ToScalar).ToList(),
        Items = p.Items.Select(ToScalar).ToList(),
        Descriptions = p.Descriptions.Select(ToText).ToList(),
        Dialogue = p.Dialogue.Select(f => new FileDto
        {
            FileName = f.FileName,
            Entries = f.Entries.Select(ToText).ToList(),
        }).ToList(),
        ImageReplacements = new Dictionary<string, string>(p.ImageReplacements),
    };

    private static TranslationProject FromDto(ProjectDto dto)
    {
        var project = new TranslationProject
        {
            LanguageName = dto.LanguageName,
            FontFace = dto.FontFace,
            FontSize = dto.FontSize,
            FontFilePath = dto.FontFilePath,
            Credits = NormalizeCredits(dto.Credits),
            SourceVersionHash = dto.SourceVersionHash,
            EditingSeconds = dto.EditingSeconds,
        };

        foreach (var s in dto.Labels) project.Labels.Add(FromScalar(s));
        foreach (var s in dto.Menus) project.Menus.Add(FromScalar(s));
        foreach (var s in dto.Speakers) project.Speakers.Add(FromScalar(s));
        foreach (var s in dto.Items) project.Items.Add(FromScalar(s));
        foreach (var t in dto.Descriptions) project.Descriptions.Add(FromText(t));

        foreach (var f in dto.Dialogue)
        {
            var file = new DialogueFile { FileName = f.FileName };
            foreach (var t in f.Entries) file.Entries.Add(FromText(t));
            project.Dialogue.Add(file);
        }

        foreach (var (key, value) in dto.ImageReplacements)
            project.ImageReplacements[key] = value;

        return project;
    }

    private static List<string> NormalizeCredits(List<string> credits)
    {
        var result = new List<string> { "", "", "" };
        for (int i = 0; i < 3 && i < credits.Count; i++)
            result[i] = credits[i] ?? "";
        return result;
    }

    private static ScalarDto ToScalar(ScalarUnit u) => new() { Key = u.Key, Source = u.Source, Target = u.Target, IsNew = u.IsNew };

    private static ScalarUnit FromScalar(ScalarDto s) => new() { Key = s.Key, Source = s.Source, Target = s.Target, IsNew = s.IsNew };

    private static TextDto ToText(TextUnit u) => new()
    {
        Id = u.Id,
        Annotation = u.Annotation,
        IsChoice = u.IsChoice,
        ChoiceGroupStart = u.ChoiceGroupStart,
        IsNew = u.IsNew,
        Source = [.. u.Source],
        Target = [.. u.Target],
    };

    private static TextUnit FromText(TextDto t) => new()
    {
        Id = t.Id,
        Annotation = t.Annotation,
        IsChoice = t.IsChoice,
        ChoiceGroupStart = t.ChoiceGroupStart,
        IsNew = t.IsNew,
        Source = t.Source,
        Target = t.Target,
    };

    // --- serialized shape ---

    private sealed class ProjectDto
    {
        public int FormatVersion { get; set; } = 1;
        public string LanguageName { get; set; } = "";
        public string FontFace { get; set; } = "GameFont";
        public int FontSize { get; set; } = 28;
        public string? FontFilePath { get; set; }
        public List<string> Credits { get; set; } = ["", "", ""];
        public string? SourceVersionHash { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long EditingSeconds { get; set; }

        public List<ScalarDto> Labels { get; set; } = [];
        public List<ScalarDto> Menus { get; set; } = [];
        public List<ScalarDto> Speakers { get; set; } = [];
        public List<ScalarDto> Items { get; set; } = [];
        public List<TextDto> Descriptions { get; set; } = [];
        public List<FileDto> Dialogue { get; set; } = [];
        public Dictionary<string, string> ImageReplacements { get; set; } = new();
    }

    private sealed class ScalarDto
    {
        public string Key { get; set; } = "";
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsNew { get; set; }
    }

    private sealed class TextDto
    {
        public string Id { get; set; } = "";
        public string Annotation { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsChoice { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ChoiceGroupStart { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsNew { get; set; }

        public List<string> Source { get; set; } = [];
        public List<string> Target { get; set; } = [];
    }

    private sealed class FileDto
    {
        public string FileName { get; set; } = "";
        public List<TextDto> Entries { get; set; } = [];
    }
}
