using System.Windows;
using System.Windows.Threading;
using FoundMySave.Core;

namespace FoundMySave;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Une langue deja choisie l'emporte sur celle de Windows.
        var saved = Settings.LoadLanguage();
        if (saved.HasValue)
            Loc.Instance.Current = saved.Value;

        // Un imprevu ne doit jamais faire disparaitre la fenetre sans explication :
        // l'utilisateur type de cet outil ne saura pas lire un journal d'erreurs.
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"{Loc.Instance["Dlg.Unexpected"]}\n\n{e.Exception.Message}\n\n{Loc.Instance["Dlg.ReadOnly"]}",
            "FoundMySave",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }
}
