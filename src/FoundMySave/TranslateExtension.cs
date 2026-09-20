using System.Windows.Data;
using System.Windows.Markup;
using FoundMySave.Core;

namespace FoundMySave;

/// <summary>
/// Permet d'ecrire Text="{local:Translate Btn.Export}" dans le XAML.
///
/// L'extension ne renvoie pas le texte mais une liaison vers l'indexeur de
/// <see cref="Loc"/> : le libelle se met donc a jour tout seul quand la langue
/// change, sans reconstruire la fenetre.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TranslateExtension : MarkupExtension
{
    public TranslateExtension() { }

    public TranslateExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
