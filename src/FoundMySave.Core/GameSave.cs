namespace FoundMySave.Core;

/// <summary>Provenance de l'installation du jeu.</summary>
public enum Platform
{
    Steam,
    GamePass
}

/// <summary>Nature du fichier trouve.</summary>
public enum SaveKind
{
    /// <summary>Un monde : commence par la signature SAVE.</summary>
    World,

    /// <summary>Un personnage : document JSON commencant par {"Version".</summary>
    Character,

    /// <summary>Autre chose : ni monde ni personnage.</summary>
    Unknown
}

/// <summary>
/// Une sauvegarde reperee sur le disque, deja decompressee si besoin.
/// </summary>
public sealed class GameSave
{
    /// <summary>Chemin du fichier d'origine, jamais modifie par l'outil.</summary>
    public required string SourcePath { get; init; }

    public required Platform Platform { get; init; }

    public required SaveKind Kind { get; init; }

    /// <summary>
    /// Nom du monde lu dans l'en-tete du fichier. Pour un personnage, son nom.
    /// Vaut null quand il n'a pas pu etre lu.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Date de sauvegarde lue dans l'en-tete, en heure UTC telle qu'ecrite par le jeu.
    /// Plus fiable que la date du fichier, qu'une copie peut fausser.
    /// </summary>
    public DateTime? SavedAt { get; init; }

    /// <summary>Taille du contenu utile, apres decompression.</summary>
    public long Size { get; init; }

    /// <summary>Taille du fichier sur le disque, avant decompression.</summary>
    public long SizeOnDisk { get; init; }

    /// <summary>Vrai quand le fichier etait compresse en zlib, ce que fait le Game Pass.</summary>
    public bool WasCompressed { get; init; }

    /// <summary>
    /// Contenu pret a etre ecrit : le .sav tel que le jeu et un serveur dedie l'attendent.
    /// </summary>
    public required byte[] Content { get; init; }

    /// <summary>
    /// Nom de fichier a utiliser a l'export. Le jeu se sert du nom du fichier comme nom
    /// de monde, c'est lui qu'on cherche dans la liste des serveurs : il ne doit plus
    /// changer ensuite, sous peine de perdre la progression.
    /// </summary>
    public string SuggestedFileName =>
        string.IsNullOrWhiteSpace(Name)
            ? Path.GetFileNameWithoutExtension(SourcePath) + ".sav"
            : Sanitize(Name!) + ".sav";

    /// <summary>Libelle court pour l'affichage.</summary>
    public string Display =>
        $"{Name ?? "(nom inconnu)"} — {Size / 1024.0:0.#} Ko — " +
        (SavedAt.HasValue ? SavedAt.Value.ToString("dd/MM/yyyy HH:mm") : "date inconnue");

    /// <summary>Retire les caracteres qu'un nom de fichier Windows n'accepte pas.</summary>
    private static string Sanitize(string name)
    {
        var cleaned = new string(name
            .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
            .ToArray())
            .Trim();

        return cleaned.Length == 0 ? "monde" : cleaned;
    }
}
