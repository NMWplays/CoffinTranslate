using CoffinTranslate.Core.Game;
using CoffinTranslate.Core.Parsing;

namespace CoffinTranslate.Core.Install;

/// <summary>Lists the translations currently installed in the game's www/languages folder.</summary>
public static class InstalledTranslationScanner
{
    public static IReadOnlyList<InstalledTranslation> Scan(GameInstallation game)
    {
        var result = new List<InstalledTranslation>();
        if (!Directory.Exists(game.LanguagesPath))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(game.LanguagesPath))
        {
            var name = Path.GetFileName(dir);
            if (name.Equals("tool", StringComparison.OrdinalIgnoreCase))
                continue;

            var dialoguePath = Directory.EnumerateFiles(dir).FirstOrDefault(f =>
            {
                var fileName = Path.GetFileName(f);
                return fileName.Equals("dialogue.txt", StringComparison.OrdinalIgnoreCase)
                       || fileName.Equals("dialogue.csv", StringComparison.OrdinalIgnoreCase);
            });

            TranslationMetadata? metadata = null;
            DialogueFormat? format = null;
            if (dialoguePath is not null)
            {
                format = Path.GetExtension(dialoguePath).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    ? DialogueFormat.Csv
                    : DialogueFormat.Txt;
                try
                {
                    var content = Utf8Text.Decode(File.ReadAllBytes(dialoguePath), out _);
                    metadata = DialogueMetadataParser.Parse(content, format.Value);
                }
                catch (IOException)
                {
                    // unreadable file — list the folder anyway so the user can remove it
                }
            }

            result.Add(new InstalledTranslation(dir, name, InstalledKind.Folder, metadata, format));
        }

        foreach (var file in Directory.EnumerateFiles(game.LanguagesPath))
        {
            if (Path.GetExtension(file).Equals(".cld", StringComparison.OrdinalIgnoreCase))
                result.Add(new InstalledTranslation(file, Path.GetFileName(file), InstalledKind.CldFile, null, DialogueFormat.Cld));
        }

        return result.OrderBy(t => t.DisplayLanguage, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
