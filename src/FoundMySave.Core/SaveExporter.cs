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
    /// Ecrit la sauvegarde accompagnee de la marche a suivre pour un serveur dedie,
    /// dans la langue demandee. Renvoie le chemin du dossier cree.
    /// </summary>
    public static string ExportForServer(GameSave save, string destinationFolder, AppLanguage language)
    {
        var suffix = language == AppLanguage.French ? "_serveur" : "_server";
        var folder = Path.Combine(destinationFolder, StripExtension(save.SuggestedFileName) + suffix);
        Directory.CreateDirectory(folder);

        File.WriteAllBytes(Path.Combine(folder, save.SuggestedFileName), save.Content);

        var guideName = language == AppLanguage.French ? "LISEZ-MOI.txt" : "README.txt";
        var guide = language == AppLanguage.French ? FrenchGuide(save) : EnglishGuide(save);
        File.WriteAllText(Path.Combine(folder, guideName), guide, new UTF8Encoding(true));

        return folder;
    }

    /// <summary>Entete commun aux deux langues : les valeurs ne se traduisent pas.</summary>
    private static void AppendFacts(StringBuilder sb, GameSave save, bool french)
    {
        var worldName = StripExtension(save.SuggestedFileName);
        var origin = save.Platform == Platform.GamePass ? "Game Pass" : "Steam";
        if (save.WasCompressed)
            origin += french ? ", decompresse par FoundMySave" : ", decompressed by FoundMySave";

        sb.AppendLine($"{(french ? "Fichier" : "File"),-13}: {save.SuggestedFileName}");
        sb.AppendLine($"{(french ? "Nom du monde" : "World name"),-13}: {worldName}");
        if (save.SavedAt.HasValue)
            sb.AppendLine($"{(french ? "Sauvegarde" : "Saved"),-13}: {save.SavedAt.Value:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"{(french ? "Taille" : "Size"),-13}: {save.Size / 1024.0:0.#} KB");
        sb.AppendLine($"{(french ? "Provenance" : "Source"),-13}: {origin}");
    }

    /// <summary>
    /// Marche a suivre pour poser un monde sur un serveur dedie. Elle suit la procedure
    /// publiee par l'editeur, dont les deux pieges : ne jamais renommer le fichier, et
    /// ne jamais modifier la configuration pendant que le serveur tourne.
    /// </summary>
    private static string FrenchGuide(GameSave save)
    {
        var worldName = StripExtension(save.SuggestedFileName);
        var sb = new StringBuilder();

        sb.AppendLine("INSTALLER CE MONDE SUR UN SERVEUR DEDIE");
        sb.AppendLine("=======================================");
        sb.AppendLine();
        AppendFacts(sb, save, french: true);
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
        sb.AppendLine("Genere par FoundMySave - https://github.com/KaineOfficial/FoundMySave");

        return sb.ToString();
    }

    private static string EnglishGuide(GameSave save)
    {
        var worldName = StripExtension(save.SuggestedFileName);
        var sb = new StringBuilder();

        sb.AppendLine("INSTALL THIS WORLD ON A DEDICATED SERVER");
        sb.AppendLine("========================================");
        sb.AppendLine();
        AppendFacts(sb, save, french: false);
        sb.AppendLine();
        sb.AppendLine("STEPS");
        sb.AppendLine("-----");
        sb.AppendLine();
        sb.AppendLine("1. Stop the server.");
        sb.AppendLine("   On Linux:  sudo systemctl stop dragonwilds");
        sb.AppendLine();
        sb.AppendLine("2. Back up, then EMPTY the server's world folder,");
        sb.AppendLine("   without deleting the folder itself:");
        sb.AppendLine("     Linux    ~/rs_server/RSDragonwilds/Saved/SaveGames/");
        sb.AppendLine("     Windows  Files\\RSDragonwilds\\Saved\\SaveGames\\");
        sb.AppendLine();
        sb.AppendLine($"3. Copy {save.SuggestedFileName} into it, WITHOUT RENAMING IT.");
        sb.AppendLine("   The file name is the world name. Renaming it afterwards");
        sb.AppendLine("   loses your progress.");
        sb.AppendLine();
        sb.AppendLine("4. In DedicatedServer.ini, set:");
        sb.AppendLine($"     DefaultWorldName={worldName}");
        sb.AppendLine("   That file lives in Saved/Config/LinuxServer/ on Linux,");
        sb.AppendLine("   Saved/Config/WindowsServer/ on Windows.");
        sb.AppendLine();
        sb.AppendLine("5. Start the server again.");
        sb.AppendLine("   On Linux:  sudo systemctl start dragonwilds");
        sb.AppendLine();
        sb.AppendLine("WORTH KNOWING");
        sb.AppendLine("-------------");
        sb.AppendLine();
        sb.AppendLine("Any change made to DedicatedServer.ini while the server is");
        sb.AppendLine("running is lost: stop it, edit, then start it again.");
        sb.AppendLine();
        sb.AppendLine($"In the public list, players search for \"{worldName}\",");
        sb.AppendLine("the WORLD name and not the server name. It is case sensitive.");
        sb.AppendLine();
        sb.AppendLine("World settings (difficulty, keep inventory on death, friendly");
        sb.AppendLine("fire) are stored inside this .sav file, not in the server");
        sb.AppendLine("configuration. That is why importing your own world is the only");
        sb.AppendLine("way to keep those settings on a dedicated server.");
        sb.AppendLine();
        sb.AppendLine("Generated by FoundMySave - https://github.com/KaineOfficial/FoundMySave");

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
