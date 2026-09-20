using FoundMySave.Core;

namespace FoundMySave;

/// <summary>
/// Retient la langue choisie d'une session a l'autre. Un simple fichier texte suffit,
/// et son absence ou son illisibilite ne doit jamais empecher l'outil de demarrer.
/// </summary>
public static class Settings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FoundMySave",
        "language.txt");

    /// <summary>Langue enregistree, ou null pour laisser celle de Windows decider.</summary>
    public static AppLanguage? LoadLanguage()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            return File.ReadAllText(FilePath).Trim().ToLowerInvariant() switch
            {
                "fr" => AppLanguage.French,
                "en" => AppLanguage.English,
                _ => null
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void SaveLanguage(AppLanguage language)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(folder);
            File.WriteAllText(FilePath, language == AppLanguage.French ? "fr" : "en");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preference non enregistree : sans consequence, l'outil reste utilisable.
        }
    }
}
