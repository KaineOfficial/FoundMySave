using FoundMySave.Core;

// Verification en console du coeur de FoundMySave.
// Sert a controler la detection, la decompression et la lecture des en-tetes
// sans passer par l'interface graphique.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("FoundMySave - verification du moteur");
Console.WriteLine(new string('=', 60));
Console.WriteLine();

var installations = SaveScanner.FindInstallations();

if (installations.Count == 0)
{
    Console.WriteLine("Aucune installation detectee.");
    return 1;
}

Console.WriteLine($"{installations.Count} installation(s) :");
foreach (var installation in installations)
    Console.WriteLine($"  [{installation.Platform}] {installation.Label}\n      {installation.SavePath}");
Console.WriteLine();

var all = new List<GameSave>();
foreach (var installation in installations)
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var found = SaveScanner.Scan(installation);
    watch.Stop();

    Console.WriteLine($"{installation.Label} : {found.Count} fichier(s) reconnu(s) en {watch.ElapsedMilliseconds} ms");
    all.AddRange(found);
}
Console.WriteLine();

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

if (worlds.Count == 0)
    problems.Add("aucun monde retenu");

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

var sample = worlds.FirstOrDefault() ?? all.FirstOrDefault(s => s.Kind == SaveKind.World);
if (sample is null)
{
    Console.WriteLine("  ignore : aucun monde disponible comme modele.");
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

if (problems.Count == 0)
{
    Console.WriteLine("RESULTAT : tout est coherent.");
    return 0;
}

Console.WriteLine("RESULTAT : anomalies detectees");
foreach (var problem in problems)
    Console.WriteLine($"  - {problem}");
return 2;

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
