namespace FoundMySave.Core;

/// <summary>Une installation du jeu reperee sur la machine.</summary>
public sealed record Installation(Platform Platform, string SavePath, string Label);

/// <summary>
/// Localise les sauvegardes des deux façons d'installer le jeu.
///
/// Steam ecrit ses mondes en clair, dans des fichiers .sav nommes d'apres le monde.
/// Le Game Pass, lui, est une application empaquetee : Windows redirige son
/// AppData\Local vers Packages\&lt;paquet&gt;\LocalCache\Local, et les mondes finissent
/// compresses dans SystemAppData\wgs sous des noms en GUID, sans extension. Ce dossier
/// n'est pas indexe par la recherche Windows, ce qui explique qu'on ne trouve jamais
/// rien en cherchant par nom ou par extension.
/// </summary>
public static class SaveScanner
{
    /// <summary>Motifs du nom de paquet. "Dominion" est le nom interne du projet Unreal.</summary>
    private static readonly string[] PackageHints = ["Dominion", "Jagex", "Dragonwilds"];

    /// <summary>Repertoires du paquet Game Pass ou une sauvegarde peut se trouver.</summary>
    private static readonly string[] PackageSubPaths =
    [
        Path.Combine("SystemAppData", "wgs"),
        Path.Combine("LocalCache", "Local", "RSDragonwilds", "Saved")
    ];

    /// <summary>Liste les installations presentes. Vide si le jeu n'a jamais ete lance.</summary>
    public static IReadOnlyList<Installation> FindInstallations()
    {
        var found = new List<Installation>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var steamPath = Path.Combine(localAppData, "RSDragonwilds", "Saved");
        if (Directory.Exists(steamPath))
            found.Add(new Installation(Platform.Steam, steamPath, "Steam / version classique"));

        var packagesRoot = Path.Combine(localAppData, "Packages");
        if (!Directory.Exists(packagesRoot))
            return found;

        foreach (var package in SafeEnumerateDirectories(packagesRoot))
        {
            var name = Path.GetFileName(package);
            if (!PackageHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)))
                continue;

            foreach (var sub in PackageSubPaths)
            {
                var candidate = Path.Combine(package, sub);
                if (Directory.Exists(candidate))
                    found.Add(new Installation(Platform.GamePass, candidate, $"Game Pass / {name}"));
            }
        }

        return found;
    }

    /// <summary>
    /// Parcourt une installation et renvoie tout ce qui est une sauvegarde. Chaque fichier
    /// est identifie par son contenu, jamais par son nom : cote Game Pass les noms ne
    /// veulent rien dire.
    /// </summary>
    public static IReadOnlyList<GameSave> Scan(Installation installation, IProgress<string>? progress = null)
    {
        var results = new List<GameSave>();

        foreach (var file in SafeEnumerateFiles(installation.SavePath))
        {
            // Les journaux du jeu sont volumineux et sans interet, on evite de les lire.
            if (file.Contains($"{Path.DirectorySeparatorChar}Logs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            progress?.Report(Path.GetFileName(file));

            var save = SaveReader.TryRead(file, installation.Platform);
            if (save is not null)
                results.Add(save);
        }

        return results;
    }

    /// <summary>
    /// Ne garde qu'une version par monde : la plus recente d'apres la date inscrite dans
    /// le fichier. Les autres sont les sauvegardes de secours du jeu, qui portent le meme
    /// nom et n'ont pas a etre proposees a l'import.
    /// </summary>
    public static IReadOnlyList<GameSave> KeepLatestPerWorld(IEnumerable<GameSave> saves)
    {
        return saves
            .Where(s => s.Kind == SaveKind.World)
            .GroupBy(s => s.Name ?? s.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(s => s.SavedAt ?? DateTime.MinValue)
                .ThenByDescending(s => s.Size)
                .First())
            .OrderByDescending(s => s.SavedAt ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>
    /// Enumere les fichiers en ignorant les dossiers interdits. L'enumeration recursive
    /// standard s'interrompt des le premier acces refuse, ce qui est frequent sous
    /// Packages : on descend donc dossier par dossier.
    /// </summary>
    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var queue = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var sub in SafeEnumerateDirectories(current))
                queue.Enqueue(sub);

            string[] files;
            try
            {
                files = Directory.GetFiles(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private static string[] SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            return [];
        }
    }
}
