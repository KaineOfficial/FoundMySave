using System.ComponentModel;
using System.Globalization;

namespace FoundMySave.Core;

public enum AppLanguage
{
    French,
    English
}

/// <summary>
/// Traduction de l'interface. Une seule instance, que les liaisons XAML observent :
/// changer <see cref="Current"/> rafraichit toute la fenetre sans la reconstruire.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private AppLanguage _current;

    private Loc()
    {
        // On suit la langue de Windows au premier lancement : un joueur francophone
        // ouvre l'outil en français, les autres en anglais.
        _current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.French
            : AppLanguage.English;
    }

    public AppLanguage Current
    {
        get => _current;
        set
        {
            if (_current == value)
                return;

            _current = value;
            // Un indexeur se rafraichit en notifiant "Item[]".
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFrench)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsFrench => _current == AppLanguage.French;
    public bool IsEnglish => _current == AppLanguage.English;

    /// <summary>Signale un changement de langue a ce que les liaisons ne couvrent pas.</summary>
    public event EventHandler? Changed;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Texte d'une clef dans la langue courante.</summary>
    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (!Table.TryGetValue(key, out var pair))
            return key;   // clef manquante : visible a l'ecran plutot que silencieuse

        return _current == AppLanguage.French ? pair.Fr : pair.En;
    }

    /// <summary>Texte d'une clef, avec des valeurs a inserer.</summary>
    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Toutes les clefs et leurs deux traductions, pour les controles automatiques.</summary>
    public static IReadOnlyDictionary<string, (string Fr, string En)> All => Table;

    private static readonly Dictionary<string, (string Fr, string En)> Table = new()
    {
        ["App.Subtitle"] = (
            "Retrouve vos mondes RuneScape: Dragonwilds, Steam comme Game Pass",
            "Finds your RuneScape: Dragonwilds worlds, on Steam and on Game Pass"),

        ["Status.Searching"] = ("Recherche en cours...", "Searching..."),
        ["Status.SearchingDetail"] = (
            "Analyse des installations Steam et Game Pass.",
            "Scanning Steam and Game Pass installations."),
        ["Status.NoInstall"] = (
            "Aucune installation du jeu detectee",
            "No installation of the game found"),
        ["Status.NoInstallDetail"] = (
            "Le jeu n'a peut-etre jamais ete lance sur ce compte Windows, ou il est installe sous un autre compte.",
            "The game may never have been launched on this Windows account, or it is installed under another one."),
        ["Status.OneInstall"] = ("Installation detectee : {0}", "Installation found: {0}"),
        ["Status.ManyInstalls"] = ("{0} installations detectees", "{0} installations found"),

        ["Empty.NoInstallTitle"] = ("Aucune installation trouvee", "No installation found"),
        ["Empty.NoInstallText"] = (
            "FoundMySave cherche dans le dossier Steam et dans les paquets Windows du Game Pass. Si vous jouez sous un autre compte Windows, lancez l'outil depuis ce compte.",
            "FoundMySave looks in the Steam folder and in the Windows packages used by Game Pass. If you play under a different Windows account, run the tool from that account."),
        ["Empty.NoWorldTitle"] = ("Aucun monde trouve", "No world found"),
        ["Empty.NoWorldBase"] = (
            "L'installation a bien ete reperee, mais aucun fichier de monde n'y figure. ",
            "The installation was found, but it contains no world file. "),
        ["Empty.NoWorldChars"] = (
            "En revanche {0} personnage(s) sont presents : vos mondes sont peut-etre uniquement dans le cloud.",
            "However {0} character(s) are present: your worlds may only exist in the cloud."),
        ["Empty.NoWorldHint"] = (
            "Creez un monde dans le jeu, quittez proprement, puis relancez la recherche.",
            "Create a world in the game, exit cleanly, then search again."),

        ["Col.World"] = ("Monde", "World"),
        ["Col.LastSave"] = ("Derniere sauvegarde", "Last saved"),
        ["Col.Size"] = ("Taille", "Size"),
        ["Col.Origin"] = ("Origine", "Source"),
        ["Col.File"] = ("Fichier source", "Source file"),

        ["Foot.Worlds"] = ("{0} monde(s)", "{0} world(s)"),
        ["Foot.Backups"] = ("{0} de secours ecartee(s)", "{0} backup(s) skipped"),
        ["Foot.Characters"] = ("{0} personnage(s)", "{0} character(s)"),

        ["Btn.Rescan"] = ("Relancer la recherche", "Search again"),
        ["Btn.ExportAll"] = ("Tout exporter", "Export all"),
        ["Btn.ExportServer"] = ("Exporter pour un serveur", "Export for a server"),
        ["Btn.Export"] = ("Exporter le monde choisi", "Export selected world"),

        ["Backup.Label"] = (
            "Copier aussi une sauvegarde de secours datee",
            "Also keep a dated backup copy"),
        ["Backup.HintOff"] = (
            "A chaque export, une copie horodatee est gardee a part. Pratique avant une mise a jour du jeu.",
            "On each export, a timestamped copy is kept aside. Handy before a game update."),
        ["Backup.HintOn"] = (
            "Une copie horodatee sera ecrite a cote du fichier exporte.",
            "A timestamped copy will be written next to the exported file."),

        ["Val.UnknownName"] = ("(nom illisible)", "(unreadable name)"),
        ["Val.UnknownDate"] = ("inconnue", "unknown"),
        ["Val.Steam"] = ("Steam", "Steam"),
        ["Val.GamePass"] = ("Game Pass", "Game Pass"),
        ["Val.GamePassZip"] = ("Game Pass, decompresse", "Game Pass, decompressed"),

        ["Dlg.WhereExport"] = ("Ou enregistrer {0} ?", "Where should {0} be saved?"),
        ["Dlg.WhereServer"] = (
            "Ou preparer le dossier pour le serveur ?",
            "Where should the server folder be prepared?"),
        ["Dlg.WhereAll"] = ("Ou enregistrer tous les mondes ?", "Where should all worlds be saved?"),
        ["Dlg.Saved"] = ("{0} enregistre.", "{0} saved."),
        ["Dlg.SavedAll"] = ("{0} monde(s) enregistre(s).", "{0} world(s) saved."),
        ["Dlg.ServerReady"] = (
            "Dossier pret pour le serveur, avec la marche a suivre dans LISEZ-MOI.txt.",
            "Server folder ready, with the step by step guide in README.txt."),
        ["Dlg.Untouched"] = (
            "Vos fichiers d'origine n'ont pas ete touches.",
            "Your original files were not touched."),
        ["Dlg.OpenFolder"] = ("Ouvrir le dossier ?", "Open the folder?"),
        ["Dlg.Failed"] = ("L'enregistrement a echoue.", "Saving failed."),
        ["Dlg.TryElsewhere"] = (
            "Essayez un autre dossier, par exemple le Bureau.",
            "Try another folder, the Desktop for instance."),
        ["Dlg.Unexpected"] = (
            "FoundMySave a rencontre un probleme inattendu.",
            "FoundMySave ran into an unexpected problem."),
        ["Dlg.ReadOnly"] = (
            "Aucun de vos fichiers n'a ete modifie : l'outil ne fait que lire et copier.",
            "None of your files were modified: the tool only reads and copies."),

        ["Lang.French"] = ("Francais", "French"),
        ["Lang.English"] = ("Anglais", "English")
    };
}
