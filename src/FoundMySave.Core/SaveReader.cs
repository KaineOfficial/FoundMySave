using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace FoundMySave.Core;

/// <summary>
/// Reconnait et lit un fichier de sauvegarde Dragonwilds, qu'il soit en clair
/// (installation Steam) ou compresse en zlib (installation Game Pass).
/// </summary>
public static partial class SaveReader
{
    /// <summary>Les quatre premiers octets d'un monde : les lettres SAVE.</summary>
    private static readonly byte[] WorldSignature = "SAVE"u8.ToArray();

    /// <summary>
    /// Deuxieme octet possible d'un en-tete zlib. Le premier vaut toujours 0x78 ; le
    /// second encode le niveau de compression. 0x9C est le cas courant.
    /// </summary>
    private static readonly byte[] ZlibSecondByte = [0x01, 0x5E, 0x9C, 0xDA];

    /// <summary>
    /// Le Game Pass fait preceder le flux zlib d'un petit en-tete. Il mesure 12 octets
    /// sur les fichiers observes, mais on cherche la signature au lieu de la supposer :
    /// l'en-tete peut changer d'une version du jeu a l'autre.
    /// </summary>
    private const int ZlibSearchWindow = 64;

    /// <summary>Zone ou le jeu ecrit la table des noms et la date. 3 Ko suffisent largement.</summary>
    private const int HeaderScanLength = 3000;

    /// <summary>
    /// Nom de la carte, identique pour tous les mondes. Le nom du monde est la derniere
    /// chaine lisible qui le precede : c'est ce reperage qui permet de renommer
    /// correctement un fichier exporte.
    /// </summary>
    private const string MapMarker = "L_World";

    /// <summary>Un fichier plus gros est ignore : aucune sauvegarde n'atteint cette taille.</summary>
    private const long MaxFileSize = 256L * 1024 * 1024;

    /// <summary>
    /// Tente de lire un fichier. Renvoie null quand ce n'est ni un monde ni un personnage,
    /// ou quand le fichier est illisible : un dossier de jeu contient beaucoup de fichiers
    /// sans rapport, et il ne faut jamais qu'un seul d'entre eux fasse echouer le scan.
    /// </summary>
    public static GameSave? TryRead(string path, Platform platform)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length == 0 || info.Length > MaxFileSize)
                return null;

            var raw = File.ReadAllBytes(path);
            var content = raw;
            var wasCompressed = false;

            // Le Game Pass stocke les mondes compresses, sous des noms sans extension.
            // On ne peut donc pas se fier au nom : on regarde ce qu'il y a dedans.
            if (!StartsWith(raw, WorldSignature))
            {
                var inflated = TryInflate(raw);
                if (inflated is not null && StartsWith(inflated, WorldSignature))
                {
                    content = inflated;
                    wasCompressed = true;
                }
            }

            if (StartsWith(content, WorldSignature))
            {
                var header = ReadHeader(content);
                return new GameSave
                {
                    SourcePath = path,
                    Platform = platform,
                    Kind = SaveKind.World,
                    Name = ExtractWorldName(header),
                    SavedAt = ExtractSavedAt(header),
                    Size = content.LongLength,
                    SizeOnDisk = info.Length,
                    WasCompressed = wasCompressed,
                    Content = content
                };
            }

            if (LooksLikeCharacter(content))
            {
                return new GameSave
                {
                    SourcePath = path,
                    Platform = platform,
                    Kind = SaveKind.Character,
                    Name = ExtractCharacterName(content),
                    SavedAt = null,
                    Size = content.LongLength,
                    SizeOnDisk = info.Length,
                    WasCompressed = wasCompressed,
                    Content = content
                };
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            // Fichier verrouille par le jeu, protege, ou trop gros pour la memoire :
            // on l'ignore sans interrompre le reste du scan.
            return null;
        }
    }

    /// <summary>
    /// Decompresse un flux zlib precede d'un en-tete de longueur inconnue.
    /// Renvoie null si aucun flux exploitable n'est trouve.
    /// </summary>
    private static byte[]? TryInflate(byte[] raw)
    {
        var limit = Math.Min(ZlibSearchWindow, raw.Length - 2);

        for (var i = 0; i < limit; i++)
        {
            if (raw[i] != 0x78 || !ZlibSecondByte.Contains(raw[i + 1]))
                continue;

            try
            {
                // Les deux octets d'en-tete zlib sont sautes : DeflateStream attend
                // le flux deflate brut qui les suit.
                using var input = new MemoryStream(raw, i + 2, raw.Length - i - 2);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);

                if (output.Length > 0)
                    return output.ToArray();
            }
            catch (InvalidDataException)
            {
                // Ces deux octets ressemblaient a du zlib par hasard. On continue de chercher.
            }
        }

        return null;
    }

    private static string ReadHeader(byte[] content)
    {
        var length = Math.Min(HeaderScanLength, content.Length);
        return Encoding.ASCII.GetString(content, 0, length);
    }

    /// <summary>
    /// Le nom du monde est la derniere chaine lisible avant "L_World". La table des noms
    /// de champs qui suit ne peut pas servir : "WorldName" y est suivi de "WorldMapName",
    /// c'est-a-dire du champ suivant et non de sa valeur.
    /// </summary>
    private static string? ExtractWorldName(string header)
    {
        var marker = header.IndexOf(MapMarker, StringComparison.Ordinal);
        if (marker <= 0)
            return null;

        var matches = PrintableRun().Matches(header[..marker]);
        if (matches.Count == 0)
            return null;

        var candidate = matches[^1].Value.Trim();
        return candidate.Length == 0 ? null : candidate;
    }

    /// <summary>
    /// La date de sauvegarde est ecrite en clair au format ISO dans l'en-tete. Elle est
    /// plus sure que la date du fichier, qu'une copie ou une restauration remet a zero.
    /// </summary>
    private static DateTime? ExtractSavedAt(string header)
    {
        var match = IsoDate().Match(header);
        if (!match.Success)
            return null;

        return DateTime.TryParse(
            match.Value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Un personnage est un document JSON qui porte une version et un bloc meta_data.</summary>
    private static bool LooksLikeCharacter(byte[] content)
    {
        if (content.Length < 32 || content[0] != (byte)'{')
            return false;

        var head = Encoding.ASCII.GetString(content, 0, Math.Min(512, content.Length));
        return head.Contains("\"Version\"", StringComparison.Ordinal)
            && head.Contains("meta_data", StringComparison.Ordinal);
    }

    private static string? ExtractCharacterName(byte[] content)
    {
        var head = Encoding.UTF8.GetString(content, 0, Math.Min(4096, content.Length));
        var match = CharacterName().Match(head);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool StartsWith(byte[] data, byte[] prefix)
    {
        if (data.Length < prefix.Length)
            return false;

        for (var i = 0; i < prefix.Length; i++)
        {
            if (data[i] != prefix[i])
                return false;
        }

        return true;
    }

    [GeneratedRegex(@"[\x20-\x7E]{3,}")]
    private static partial Regex PrintableRun();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"""(?:char_name|name)""\s*:\s*""([^""]{1,64})""", RegexOptions.IgnoreCase)]
    private static partial Regex CharacterName();
}
