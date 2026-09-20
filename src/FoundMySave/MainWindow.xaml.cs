using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FoundMySave.Core;
using Microsoft.Win32;

namespace FoundMySave;

/// <summary>
/// Une ligne de la liste, mise en forme pour l'affichage. Les libelles sont figes a
/// la construction : la liste est reconstruite quand la langue change.
/// </summary>
public sealed class WorldRow(GameSave save)
{
    private static Loc L => Loc.Instance;

    public GameSave Save { get; } = save;

    public string NameDisplay => Save.Name ?? L["Val.UnknownName"];

    public string SavedDisplay => Save.SavedAt.HasValue
        ? Save.SavedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        : L["Val.UnknownDate"];

    public string SizeDisplay => $"{Save.Size / 1024.0:0.#} Ko";

    public string OriginDisplay => Save.Platform == Platform.GamePass
        ? (Save.WasCompressed ? L["Val.GamePassZip"] : L["Val.GamePass"])
        : L["Val.Steam"];

    public string SourceDisplay => Path.GetFileName(Save.SourcePath);
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<WorldRow> _rows = [];

    // Conserves pour pouvoir reconstruire l'affichage quand la langue change,
    // sans relancer une recherche sur le disque.
    private IReadOnlyList<Installation> _installations = [];
    private IReadOnlyList<GameSave> _saves = [];

    private static Loc L => Loc.Instance;

    public MainWindow()
    {
        InitializeComponent();
        WorldList.ItemsSource = _rows;
        UpdateLanguageButtons();
        Loaded += async (_, _) => await ScanAsync();
    }

    /// <summary>
    /// Lance la recherche hors du fil d'affichage : parcourir le dossier des paquets
    /// prend plusieurs secondes et figerait la fenetre.
    /// </summary>
    private async Task ScanAsync()
    {
        RescanButton.IsEnabled = false;
        StatusTitle.Text = L["Status.Searching"];
        StatusDetail.Text = L["Status.SearchingDetail"];
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

        _installations = installations;
        _saves = saves;

        Refresh();
        RescanButton.IsEnabled = true;
    }

    /// <summary>Reconstruit tout l'affichage a partir des resultats deja en memoire.</summary>
    private void Refresh()
    {
        var worlds = SaveScanner.KeepLatestPerWorld(_saves);

        _rows.Clear();
        foreach (var world in worlds)
            _rows.Add(new WorldRow(world));

        UpdateStatus(worlds.Count);
        UpdateBackupHint();
        UpdateButtons();
    }

    private void UpdateStatus(int worldCount)
    {
        var characters = _saves.Count(s => s.Kind == SaveKind.Character);
        var backups = _saves.Count(s => s.Kind == SaveKind.World) - worldCount;

        if (_installations.Count == 0)
        {
            StatusTitle.Text = L["Status.NoInstall"];
            StatusDetail.Text = L["Status.NoInstallDetail"];
            ShowEmpty(L["Empty.NoInstallTitle"], L["Empty.NoInstallText"]);
            return;
        }

        StatusTitle.Text = _installations.Count == 1
            ? L.Format("Status.OneInstall", _installations[0].Label)
            : L.Format("Status.ManyInstalls", _installations.Count);

        StatusDetail.Text = string.Join("\n", _installations.Select(i => $"{i.Label}  -  {i.SavePath}"));

        if (worldCount == 0)
        {
            var text = L["Empty.NoWorldBase"] + (characters > 0
                ? L.Format("Empty.NoWorldChars", characters)
                : L["Empty.NoWorldHint"]);

            ShowEmpty(L["Empty.NoWorldTitle"], text);
            return;
        }

        EmptyPanel.Visibility = Visibility.Collapsed;

        // Texte volontairement court : la place se reduit quand les boutons s'elargissent.
        var parts = new List<string> { L.Format("Foot.Worlds", worldCount) };
        if (backups > 0) parts.Add(L.Format("Foot.Backups", backups));
        if (characters > 0) parts.Add(L.Format("Foot.Characters", characters));
        FooterText.Text = string.Join("  |  ", parts);
    }

    private void ShowEmpty(string title, string text)
    {
        EmptyTitle.Text = title;
        EmptyText.Text = text;
        EmptyPanel.Visibility = Visibility.Visible;
        FooterText.Text = string.Empty;
    }

    private void UpdateButtons()
    {
        var hasSelection = WorldList.SelectedItem is WorldRow;
        ExportButton.IsEnabled = hasSelection;
        ExportServerButton.IsEnabled = hasSelection;
        ExportAllButton.IsEnabled = _rows.Count > 0;
    }

    private void UpdateBackupHint() =>
        AutoBackupHint.Text = AutoBackupCheck.IsChecked == true ? L["Backup.HintOn"] : L["Backup.HintOff"];

    private void UpdateLanguageButtons()
    {
        FrButton.IsChecked = L.IsFrench;
        EnButton.IsChecked = L.IsEnglish;
    }

    private void SwitchTo(AppLanguage language)
    {
        L.Current = language;
        Settings.SaveLanguage(language);
        UpdateLanguageButtons();
        Refresh();   // les libelles des lignes sont figes, il faut les reconstruire
    }

    private void OnFrench(object sender, RoutedEventArgs e) => SwitchTo(AppLanguage.French);

    private void OnEnglish(object sender, RoutedEventArgs e) => SwitchTo(AppLanguage.English);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private async void OnRescan(object sender, RoutedEventArgs e) => await ScanAsync();

    private void OnAutoBackupToggled(object sender, RoutedEventArgs e) => UpdateBackupHint();

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (WorldList.SelectedItem is not WorldRow row)
            return;

        var folder = AskFolder(L.Format("Dlg.WhereExport", row.Save.SuggestedFileName));
        if (folder is null)
            return;

        try
        {
            var path = SaveExporter.Export(row.Save, folder);
            WriteBackupIfAsked(row.Save, folder);
            Done(L.Format("Dlg.Saved", Path.GetFileName(path)), folder);
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

        var folder = AskFolder(L["Dlg.WhereServer"]);
        if (folder is null)
            return;

        try
        {
            var created = SaveExporter.ExportForServer(row.Save, folder, L.Current);
            WriteBackupIfAsked(row.Save, created);
            Done(L["Dlg.ServerReady"], created);
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

        var folder = AskFolder(L["Dlg.WhereAll"]);
        if (folder is null)
            return;

        try
        {
            foreach (var row in _rows)
            {
                SaveExporter.Export(row.Save, folder);
                WriteBackupIfAsked(row.Save, folder);
            }

            Done(L.Format("Dlg.SavedAll", _rows.Count), folder);
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

        var backupFolder = Path.Combine(folder, L.Current == AppLanguage.French ? "sauvegardes" : "backups");
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
            $"{message}\n\n{L["Dlg.Untouched"]}\n\n{L["Dlg.OpenFolder"]}",
            "FoundMySave",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private static void Failed(Exception ex)
    {
        MessageBox.Show(
            $"{L["Dlg.Failed"]}\n\n{ex.Message}\n\n{L["Dlg.TryElsewhere"]}",
            "FoundMySave",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
