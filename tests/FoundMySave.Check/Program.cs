using FoundMySave.Core;

// Verification en console du coeur de FoundMySave.
// Sert a controler la detection, la decompression et la lecture des en-tetes
// sans passer par l'interface graphique.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("FoundMySave - verification du moteur");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

var installations = SaveScanner.FindInstallations();
var all = new List<GameSave>();

// L'absence du jeu n'est pas une anomalie : sur une machine de compilation, seul
// l'auto-test plus bas a un sens, et lui n'a besoin de rien d'installe.
if (installations.Count == 0)
{
    Console.WriteLine("Aucune installation du jeu sur cette machine.");
    Console.WriteLine("Seul l'auto-test du format sera execute.");
    Console.WriteLine();
}
else
{
    Console.WriteLine($"{installations.Count} installation(s) :");
    foreach (var installation in installations)
        Console.WriteLine($"  [{installation.Platform}] {installation.Label}\n      {installation.SavePath}");
    Console.WriteLine();

    foreach (var installation in installations)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var found = SaveScanner.Scan(installation);
        watch.Stop();

        Console.WriteLine($"{installation.Label} : {found.Count} fichier(s) reconnu(s) en {watch.ElapsedMilliseconds} ms");
        all.AddRange(found);
    }
    Console.WriteLine();
}

Console.WriteLine("TOUT CE QUI A ETE RECONNU");
Console.WriteLine(new string('-', 60));
foreach (var save in all.OrderByDescending(s => s.Size))
{
    var compressed = save.WasCompressed ? "  [decompresse]" : "";
    Console.WriteLine($"  {save.Kind,-9} {save.Name ?? "(?)",-22} {save.Size / 1024.0,9:0.#} Ko  " +
                      $"{(save.SavedAt.HasValue ? save.SavedAt.Value.ToString("dd/MM/yyyy HH:mm") : "date ?"),-17}" +
                      $"{Path.GetFileName(save.SourcePath)}{compressed}");
}
Console.WriteLine();

var worlds = SaveScanner.KeepLatestPerWorld(all);
Console.WriteLine("MONDES RETENUS (le plus recent de chaque)");
Console.WriteLine(new string('-', 60));
foreach (var world in worlds)
    Console.WriteLine($"  {world.SuggestedFileName,-28} {world.Size / 1024.0,9:0.#} Ko   {world.Display}");
Console.WriteLine();

// Controles de coherence : ce sont eux qui disent si le moteur fait vraiment son travail.
var problems = new List<string>();

// Un monde manquant n'est une anomalie que si une installation a ete trouvee.
if (worlds.Count == 0 && installations.Count > 0 && all.Count > 0)
    problems.Add("des fichiers ont ete reconnus mais aucun monde n'a ete retenu");

foreach (var world in worlds)
{
    if (world.Content.Length < 4 || System.Text.Encoding.ASCII.GetString(world.Content, 0, 4) != "SAVE")
        problems.Add($"{world.SuggestedFileName} : signature SAVE absente du contenu exporte");

    if (string.IsNullOrWhiteSpace(world.Name))
        problems.Add($"{Path.GetFileName(world.SourcePath)} : nom du monde illisible");

    if (!world.SavedAt.HasValue)
        problems.Add($"{world.SuggestedFileName} : date de sauvegarde illisible");
}

// Le cas Game Pass ne peut pas etre teste sur une machine qui n'a que Steam.
// On fabrique donc un fichier au format observe chez un joueur Game Pass :
// un en-tete de 12 octets, puis le monde compresse en zlib, le tout sous un nom
// en GUID sans extension. Si le moteur le relit, la partie Game Pass tient.
Console.WriteLine("AUTO-TEST DU FORMAT GAME PASS");
Console.WriteLine(new string('-', 60));

// A defaut de vrai monde, on en fabrique un minimal : l'auto-test tourne ainsi
// partout, y compris sur une machine de compilation ou le jeu n'est pas installe.
var sampleContent = worlds.FirstOrDefault()?.Content
                    ?? all.FirstOrDefault(s => s.Kind == SaveKind.World)?.Content
                    ?? BuildSyntheticWorld("MondeDeTest", new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

var sample = SaveReader.TryRead(WriteTemp(sampleContent), Platform.Steam);

if (sample is null)
{
    problems.Add("auto-test : le monde de reference n'a pas ete reconnu");
}
else
{
    var sandbox = Path.Combine(Path.GetTempPath(), "FoundMySave.SelfTest", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(sandbox);

    try
    {
        var fake = Path.Combine(sandbox, Guid.NewGuid().ToString("N").ToUpperInvariant());
        File.WriteAllBytes(fake, PackLikeGamePass(sample.Content));

        var reread = SaveReader.TryRead(fake, Platform.GamePass);

        if (reread is null)
            problems.Add("format Game Pass : le fichier compresse n'a pas ete reconnu");
        else if (!reread.WasCompressed)
            problems.Add("format Game Pass : la decompression n'a pas ete signalee");
        else if (!reread.Content.SequenceEqual(sample.Content))
            problems.Add("format Game Pass : le contenu restitue differe de l'original");
        else if (reread.Name != sample.Name)
            problems.Add($"format Game Pass : nom lu '{reread.Name}' au lieu de '{sample.Name}'");
        else
            Console.WriteLine($"  OK : '{reread.Name}' restitue a l'identique " +
                              $"({reread.Content.Length} octets), depuis un fichier sans extension.");
    }
    finally
    {
        try { Directory.Delete(sandbox, recursive: true); } catch { /* sans consequence */ }
    }
}
Console.WriteLine();

// Controle des traductions. Une clef absente s'afficherait telle quelle dans la
// fenetre, et un {0} oublie d'un cote ferait disparaitre une valeur : ces deux
// defauts passent inapercus a la relecture, d'ou ce controle automatique.
Console.WriteLine("CONTROLE DES TRADUCTIONS");
Console.WriteLine(new string('-', 60));

var placeholder = new System.Text.RegularExpressions.Regex(@"\{\d+\}");
foreach (var (key, pair) in Loc.All)
{
    if (string.IsNullOrWhiteSpace(pair.Fr)) problems.Add($"traduction : '{key}' vide en francais");
    if (string.IsNullOrWhiteSpace(pair.En)) problems.Add($"traduction : '{key}' vide en anglais");

    var fr = placeholder.Matches(pair.Fr).Select(m => m.Value).OrderBy(v => v).ToArray();
    var en = placeholder.Matches(pair.En).Select(m => m.Value).OrderBy(v => v).ToArray();
    if (!fr.SequenceEqual(en))
        problems.Add($"traduction : '{key}' n'a pas les memes valeurs a inserer ({string.Join(",", fr)} / {string.Join(",", en)})");
}
Console.WriteLine($"  {Loc.All.Count} clefs verifiees dans les deux langues.");
Console.WriteLine();

// Le dossier de travail de l'auto-test n'a plus lieu d'etre.
try { Directory.Delete(Path.Combine(Path.GetTempPath(), "FoundMySave.SelfTest"), recursive: true); }
catch { /* sans consequence */ }

if (problems.Count == 0)
{
    Console.WriteLine("RESULTAT : tout est coherent.");
    return 0;
}

Console.WriteLine("RESULTAT : anomalies detectees");
foreach (var problem in problems)
    Console.WriteLine($"  - {problem}");
return 2;

// Ecrit un contenu dans un fichier temporaire et renvoie son chemin.
static string WriteTemp(byte[] content)
{
    var folder = Path.Combine(Path.GetTempPath(), "FoundMySave.SelfTest");
    Directory.CreateDirectory(folder);

    var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".sav");
    File.WriteAllBytes(path, content);
    return path;
}

/// <summary>
/// Fabrique un monde minimal mais conforme a ce que le moteur attend : la signature
/// SAVE, une date ISO, puis le nom du monde juste avant le marqueur L_World.
/// Les champs sont separes par des octets nuls, comme dans un vrai fichier, sans quoi
/// les chaines se toucheraient et le nom serait mal lu.
/// </summary>
static byte[] BuildSyntheticWorld(string worldName, DateTime savedAt)
{
    var output = new MemoryStream();

    void Write(string text)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        output.Write(bytes, 0, bytes.Length);
    }

    void Separator() => output.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);

    Write("SAVE");
    Separator();
    Write("INFO");
    Separator();
    Write(savedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    Separator();
    Write(worldName);
    Separator();
    Write("L_World");
    Separator();

    // Un peu de corps, pour que le fichier ne soit pas degenere.
    output.Write(new byte[2048], 0, 2048);

    return output.ToArray();
}

// Reproduit l'enveloppe du Game Pass : 12 octets quelconques, puis un flux zlib.
static byte[] PackLikeGamePass(byte[] content)
{
    using var compressed = new MemoryStream();
    using (var deflate = new System.IO.Compression.DeflateStream(
               compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
    {
        deflate.Write(content, 0, content.Length);
    }

    var body = compressed.ToArray();
    var packed = new byte[12 + 2 + body.Length];

    // En-tete opaque, non identifie : le moteur cherche la signature zlib plutot
    // que de supposer une longueur fixe, donc son contenu importe peu.
    for (var i = 0; i < 12; i++)
        packed[i] = (byte)(i + 1);

    packed[12] = 0x78;   // signature zlib
    packed[13] = 0x9C;   // niveau de compression courant
    Array.Copy(body, 0, packed, 14, body.Length);

    return packed;
}
