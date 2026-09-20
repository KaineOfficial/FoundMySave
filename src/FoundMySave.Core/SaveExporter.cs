using System.Text;

namespace FoundMySave.Core;

/// <summary>
/// Ecrit les sauvegardes sur le disque, et produit la marche a suivre pour les
/// installer sur un serveur dedie.
/// </summary>
public static class SaveExporter
{
    /// <summary>
    /// Ecrit une sauvegarde dans un dossier, sous le nom que le jeu attend.
    /// Ne remplace jamais un fichier existant sans le dire : un suffixe est ajoute.
    /// </summary>
    public static string Export(GameSave save, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);

        var target = Path.Combine(destinationFolder, save.SuggestedFileName);
        target = MakeUnique(target);

        File.WriteAllBytes(target, save.Content);
        return target;
    }

    /// <summary>
    /// Ecrit la sauvegarde accompagnee de la marche a suivre pour un serveur dedie.
    /// Renvoie le chemin du dossier cree.
    /// </summary>
    public static string ExportForServer(GameSave save, string destinationFolder)
    {
        var folder = Path.Combine(destinationFolder, StripExtension(save.SuggestedFileName) + "_serveur");
        Directory.CreateDirectory(folder);

        File.WriteAllBytes(Path.Combine(folder, save.SuggestedFileName), save.Content);
        File.WriteAllText(Path.Combine(folder, "LISEZ-MOI.txt"), BuildServerInstructions(save), new UTF8Encoding(true));

        return folder;
    }

    /// <summary>
    /// Marche a suivre pour poser un monde sur un serveur dedie. Elle suit la procedure
    /// publiee par l'editeur, dont les deux pieges : ne jamais renommer le fichier, et
    /// ne jamais modifier la configuration pendant que le serveur tourne.
    /// </summary>
    private static string BuildServerInstructions(GameSave save)
    {
        var worldName = StripExtension(save.SuggestedFileName);

        var sb = new StringBuilder();
        sb.AppendLine("INSTALLER CE MONDE SUR UN SERVEUR DEDIE");
        sb.AppendLine("=======================================");
        sb.AppendLine();
        sb.AppendLine($"Fichier      : {save.SuggestedFileName}");
        sb.AppendLine($"Nom du monde : {worldName}");
        if (save.SavedAt.HasValue)
            sb.AppendLine($"Sauvegarde   : {save.SavedAt.Value:dd/MM/yyyy HH:mm} (UTC)");
        sb.AppendLine($"Taille       : {save.Size / 1024.0:0.#} Ko");
        sb.AppendLine($"Provenance   : {(save.Platform == Platform.GamePass ? "Game Pass" : "Steam")}"
                      + (save.WasCompressed ? ", decompresse par FoundMySave" : ""));
        sb.AppendLine();
        sb.AppendLine("ETAPES");
        sb.AppendLine("------");
        sb.AppendLine();
        sb.AppendLine("1. Arreter le serveur.");
        sb.AppendLine("   Sous Linux :  sudo systemctl stop dragonwilds");
        sb.AppendLine();
        sb.AppendLine("2. Sauvegarder puis VIDER le dossier des mondes du serveur,");
        sb.AppendLine("   sans supprimer le dossier lui-meme :");
        sb.AppendLine("     Linux   ~/rs_server/RSDragonwilds/Saved/SaveGames/");
        sb.AppendLine("     Windows Files\\RSDragonwilds\\Saved\\SaveGames\\");
        sb.AppendLine();
        sb.AppendLine($"3. Copier {save.SuggestedFileName} dedans, SANS LE RENOMMER.");
        sb.AppendLine("   Le nom du fichier est le nom du monde. Le changer apres coup");
        sb.AppendLine("   fait perdre la progression.");
        sb.AppendLine();
        sb.AppendLine("4. Dans DedicatedServer.ini, mettre :");
        sb.AppendLine($"     DefaultWorldName={worldName}");
        sb.AppendLine("   Le fichier se trouve dans Saved/Config/LinuxServer/ sous Linux,");
        sb.AppendLine("   Saved/Config/WindowsServer/ sous Windows.");
        sb.AppendLine();
        sb.AppendLine("5. Redemarrer le serveur.");
        sb.AppendLine("   Sous Linux :  sudo systemctl start dragonwilds");
        sb.AppendLine();
        sb.AppendLine("A SAVOIR");
        sb.AppendLine("--------");
        sb.AppendLine();
        sb.AppendLine("Toute modification de DedicatedServer.ini pendant que le serveur");
        sb.AppendLine("tourne est perdue : arreter, editer, puis redemarrer.");
        sb.AppendLine();
        sb.AppendLine($"Dans la liste publique, les joueurs cherchent \"{worldName}\",");
        sb.AppendLine("le nom du MONDE et non celui du serveur. La casse compte.");
        sb.AppendLine();
        sb.AppendLine("Les reglages du monde (difficulte, inventaire garde a la mort,");
        sb.AppendLine("tir allie) sont enregistres dans ce fichier .sav, pas dans la");
        sb.AppendLine("configuration du serveur. C'est pourquoi importer son monde est");
        sb.AppendLine("le seul moyen de les retrouver sur un serveur dedie.");
        sb.AppendLine();
        sb.AppendLine("Genere par FoundMySave — https://github.com/KaineOfficial/FoundMySave");

        return sb.ToString();
    }

    private static string StripExtension(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName);

    /// <summary>Ajoute un suffixe numerique tant que le nom est deja pris.</summary>
    private static string MakeUnique(string path)
    {
        if (!File.Exists(path))
            return path;

        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(folder, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(folder, $"{name} ({Guid.NewGuid():N}){ext}");
    }
}
