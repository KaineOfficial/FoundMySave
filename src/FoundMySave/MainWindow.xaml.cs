using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FoundMySave.Core;
using Microsoft.Win32;

namespace FoundMySave;

/// <summary>Une ligne de la liste, mise en forme pour l'affichage.</summary>
public sealed class WorldRow(GameSave save)
{
    public GameSave Save { get; } = save;

    public string NameDisplay => Save.Name ?? "(nom illisible)";

    public string SavedDisplay => Save.SavedAt.HasValue
        ? Save.SavedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        : "inconnue";

    public string SizeDisplay => $"{Save.Size / 1024.0:0.#} Ko";

    public string OriginDisplay => Save.Platform == Platform.GamePass
        ? (Save.WasCompressed ? "Game Pass, decompresse" : "Game Pass")
        : "Steam";

    public string SourceDisplay => Path.GetFileName(Save.SourcePath);
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<WorldRow> _rows = [];

    public MainWindow()
    {
        InitializeComponent();
        WorldList.ItemsSource = _rows;
        Loaded += async (_, _) => await ScanAsync();
    }

    /// <summary>
    /// Lance la recherche hors du fil d'affichage : parcourir le dossier des paquets
    /// prend plusieurs secondes et figerait la fenetre.
    /// </summary>
    private async Task ScanAsync()
    {
        SetBusy(true);
        StatusTitle.Text = "Recherche en cours...";
        StatusDetail.Text = "Analyse des installations Steam et Game Pass.";
        _rows.Clear();
        EmptyPanel.Visibility = Visibility.Collapsed;

        var (installations, saves) = await Task.Run(() =>
        {
            var found = SaveScanner.FindInstallations();
            var all = new List<GameSave>();

            foreach (var installation in found)
                all.AddRange(SaveScanner.Scan(installation));

            return (found, (IReadOnlyList<GameSave>)all);
        });

        var worlds = SaveScanner.KeepLatestPerWorld(saves);

        foreach (var world in worlds)
            _rows.Add(new WorldRow(world));

        UpdateStatus(installations, saves, worlds.Count);
        SetBusy(false);
    }

    private void UpdateStatus(IReadOnlyList<Installation> installations, IReadOnlyList<GameSave> saves, int worldCount)
    {
        var characters = saves.Count(s => s.Kind == SaveKind.Character);
        var backups = saves.Count(s => s.Kind == SaveKind.World) - worldCount;

        if (installations.Count == 0)
        {
            StatusTitle.Text = "Aucune installation du jeu detectee";
            StatusDetail.Text = "Le jeu n'a peut-etre jamais ete lance sur ce compte Windows, "
                                + "ou il est installe sous un autre compte.";
            ShowEmpty("Aucune installation trouvee",
                "FoundMySave cherche dans le dossier Steam et dans les paquets Windows du Game Pass. "
                + "Si vous jouez sous un autre compte Windows, lancez l'outil depuis ce compte.");
            return;
        }

        StatusTitle.Text = installations.Count == 1
            ? $"Installation detectee : {installations[0].Label}"
            : $"{installations.Count} installations detectees";

        StatusDetail.Text = string.Join("\n", installations.Select(i => $"{i.Label}  -  {i.SavePath}"));

        if (worldCount == 0)
        {
            ShowEmpty("Aucun monde trouve",
                "L'installation a bien ete reperee, mais aucun fichier de monde n'y figure. "
                + (characters > 0
                    ? $"En revanche {characters} personnage(s) sont presents : vos mondes sont peut-etre uniquement dans le cloud."
                    : "Creez un monde dans le jeu, quittez proprement, puis relancez la recherche."));
            return;
        }

        EmptyPanel.Visibility = Visibility.Collapsed;

        var parts = new List<string> { $"{worldCount} monde(s)" };
        if (backups > 0) parts.Add($"{backups} sauvegarde(s) de secours ecartee(s)");
        if (characters > 0) parts.Add($"{characters} personnage(s)");
        FooterText.Text = string.Join("   |   ", parts);
    }

    private void ShowEmpty(string title, string text)
    {
        EmptyTitle.Text = title;
        EmptyText.Text = text;
        EmptyPanel.Visibility = Visibility.Visible;
        FooterText.Text = string.Empty;
    }

    private void SetBusy(bool busy)
    {
        RescanButton.IsEnabled = !busy;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasSelection = WorldList.SelectedItem is WorldRow;
        ExportButton.IsEnabled = hasSelection;
        ExportServerButton.IsEnabled = hasSelection;
        ExportAllButton.IsEnabled = _rows.Count > 0;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private async void OnRescan(object sender, RoutedEventArgs e) => await ScanAsync();

    private void OnAutoBackupToggled(object sender, RoutedEventArgs e)
    {
        AutoBackupHint.Text = AutoBackupCheck.IsChecked == true
            ? "Une copie horodatee sera ecrite a cote du fichier exporte."
            : "A chaque export, une copie horodatee est gardee a part. Pratique avant une mise a jour du jeu.";
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (WorldList.SelectedItem is not WorldRow row)
            return;

        var folder = AskFolder($"Ou enregistrer {row.Save.SuggestedFileName} ?");
        if (folder is null)
            return;

        try
        {
            var path = SaveExporter.Export(row.Save, folder);
            WriteBackupIfAsked(row.Save, folder);
            Done($"{Path.GetFileName(path)} enregistre.", folder);
        }
        catch (Exception ex)
        {
            Failed(ex);
        }
    }

    private void OnExportForServer(object sender, RoutedEventArgs e)
    {
        if (WorldList.SelectedItem is not WorldRow row)
            return;

        var folder = AskFolder("Ou preparer le dossier pour le serveur ?");
        if (folder is null)
            return;

        try
        {
            var created = SaveExporter.ExportForServer(row.Save, folder);
            WriteBackupIfAsked(row.Save, created);
            Done("Dossier pret pour le serveur, avec la marche a suivre dans LISEZ-MOI.txt.", created);
        }
        catch (Exception ex)
        {
            Failed(ex);
        }
    }

    private void OnExportAll(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
            return;

        var folder = AskFolder("Ou enregistrer tous les mondes ?");
        if (folder is null)
            return;

        try
        {
            foreach (var row in _rows)
            {
                SaveExporter.Export(row.Save, folder);
                WriteBackupIfAsked(row.Save, folder);
            }

            Done($"{_rows.Count} monde(s) enregistre(s).", folder);
        }
        catch (Exception ex)
        {
            Failed(ex);
        }
    }

    /// <summary>Copie horodatee, pour garder un etat avant une mise a jour du jeu.</summary>
    private void WriteBackupIfAsked(GameSave save, string folder)
    {
        if (AutoBackupCheck.IsChecked != true)
            return;

        var backupFolder = Path.Combine(folder, "sauvegardes");
        Directory.CreateDirectory(backupFolder);

        var stamp = (save.SavedAt ?? DateTime.UtcNow).ToLocalTime().ToString("yyyyMMdd-HHmm");
        var name = Path.GetFileNameWithoutExtension(save.SuggestedFileName);
        File.WriteAllBytes(Path.Combine(backupFolder, $"{name}_{stamp}.sav"), save.Content);
    }

    /// <summary>
    /// Demande un dossier. OpenFolderDialog evite l'ancien selecteur de WinForms et
    /// n'ajoute aucune dependance.
    /// </summary>
    private string? AskFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void Done(string message, string folder)
    {
        var result = MessageBox.Show(
            message + "\n\nVos fichiers d'origine n'ont pas ete touches.\n\nOuvrir le dossier ?",
            "FoundMySave",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private static void Failed(Exception ex)
    {
        MessageBox.Show(
            "L'enregistrement a echoue.\n\n" + ex.Message +
            "\n\nEssayez un autre dossier, par exemple le Bureau.",
            "FoundMySave",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
