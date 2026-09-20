using System.Windows;
using System.Windows.Threading;

namespace FoundMySave;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Un imprevu ne doit jamais faire disparaitre la fenetre sans explication :
        // l'utilisateur type de cet outil ne saura pas lire un journal d'erreurs.
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "FoundMySave a rencontre un probleme inattendu.\n\n" +
            e.Exception.Message +
            "\n\nAucun de vos fichiers n'a ete modifie : l'outil ne fait que lire et copier.",
            "FoundMySave",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }
}
