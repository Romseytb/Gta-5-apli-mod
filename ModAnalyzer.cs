using System.Text;
using System.Text.RegularExpressions;

namespace GtaModsManager.Core;

public static class ModAnalyzer
{
    static readonly string[] ResourceExt = { ".yft", ".ytd", ".ydd", ".ydr", ".ymt", ".ytyp", ".ymap" };
    static readonly string[] TextExt = { ".txt", ".md", ".nfo" };

    /// <summary>Destinations connues dans le jeu (Legacy) pour certains véhicules de base. À confirmer par l'utilisateur.</summary>
    static readonly Dictionary<string, string> KnownReplaceTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        { "pbus", @"x64e.rpf\levels\gta5\vehicles.rpf" },
    };

    public static string KnownTarget(string model)
        => model != null && KnownReplaceTargets.TryGetValue(model, out var t) ? t : null;

    /// <summary>Analyse en LECTURE SEULE : extrait le ZIP dans un dossier de travail, ne touche jamais au jeu.</summary>
    public static ModPackage Analyze(string sourcePath, string workRoot)
    {
        var pkg = new ModPackage { Source = sourcePath };
        string root;
        if (Directory.Exists(sourcePath)) root = sourcePath;
        else if (File.Exists(sourcePath) && sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            root = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
            SafeZip.Extract(sourcePath, root);
        }
        else if (File.Exists(sourcePath))
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            pkg.Messages.Add(ext is ".rar" or ".7z"
                ? "Les archives " + ext + " ne sont pas encore prises en charge. Décompressez-les (ou recompressez en ZIP) puis glissez le résultat."
                : "Ce type de fichier (" + ext + ") n'est pas pris en charge directement. Glissez un ZIP ou un dossier de mod.");
            return pkg;
        }
        else { pkg.Messages.Add("Fichier ou dossier introuvable."); return pkg; }

        pkg.WorkDir = root;
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
        var texts = files.Where(f => TextExt.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();

        var dlcDirs = files.Where(f => Path.GetFileName(f).Equals("dlc.rpf", StringComparison.OrdinalIgnoreCase))
                           .Select(f => Path.GetDirectoryName(f)!).Distinct(StringComparer.OrdinalIgnoreCase)
                           .OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var d in dlcDirs) pkg.Units.Add(BuildDlcUnit(d, texts));

        var resDirs = files.Where(f => ResourceExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                           .Select(f => Path.GetDirectoryName(f)!).Distinct(StringComparer.OrdinalIgnoreCase)
                           .Where(d => !dlcDirs.Any(x => IsUnder(x, d)))
                           .OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var d in resDirs) pkg.Units.Add(BuildReplaceUnit(d));

        // Variantes d'un même véhicule (ex. emc_svj63 / emc_svj63b)
        var dlcUnits = pkg.Units.Where(u => u.Kind == ModKind.AddOnDlc).ToList();
        foreach (var a in dlcUnits)
            foreach (var b in dlcUnits)
                if (a != b && b.Name.StartsWith(a.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var msg = "« " + a.Name + " » et « " + b.Name + " » ressemblent à deux variantes du même véhicule. N'en installez qu'une à la fois, sauf si l'auteur indique qu'elles peuvent coexister.";
                    if (!a.Warnings.Contains(msg)) a.Warnings.Add(msg);
                    if (!b.Warnings.Contains(msg)) b.Warnings.Add(msg);
                }

        if (pkg.Units.Count == 0)
            pkg.Messages.Add("Je n'ai reconnu aucun mod GTA V connu dans ce contenu (ni dlc.rpf, ni fichiers .yft/.ytd/.ydd).");
        return pkg;
    }

    static bool IsUnder(string parent, string child)
    {
        var p = parent.TrimEnd('\\', '/');
        return child.Equals(p, StringComparison.OrdinalIgnoreCase)
            || child.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || child.StartsWith(p + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    static string ReadText(string path)
    {
        try
        {
            var b = File.ReadAllBytes(path);
            if (b.Length > 500_000) Array.Resize(ref b, 500_000);
            return Encoding.UTF8.GetString(b);
        }
        catch { return ""; }
    }

    static ModUnit BuildDlcUnit(string dir, List<string> allTexts)
    {
        var u = new ModUnit { Kind = ModKind.AddOnDlc, SourceDir = dir, Name = new DirectoryInfo(dir).Name };
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            u.Files.Add(Path.GetRelativePath(dir, f));

        // Vérification de l'en-tête de dlc.rpf (RPF7 ; "OPEN" = non chiffré)
        try
        {
            using var fs = File.OpenRead(Path.Combine(dir, "dlc.rpf"));
            var h = new byte[16];
            if (fs.Read(h, 0, 16) == 16 && BitConverter.ToUInt32(h, 0) == 0x52504637)
                u.Notes.Add("dlc.rpf : archive RPF7 valide (format GTA V).");
            else
                u.Warnings.Add("dlc.rpf ne ressemble pas à une archive RPF7 de GTA V : le fichier est peut-être endommagé ou destiné à un autre jeu.");
        }
        catch { u.Warnings.Add("Impossible de lire dlc.rpf."); }

        // Notices : celles du dossier et de ses dossiers parents
        var related = allTexts.Where(t => IsUnder(Path.GetDirectoryName(t)!, dir)).ToList();
        var corpus = string.Join("\n", related.Select(ReadText));
        if (corpus.Contains("vehfuncs", StringComparison.OrdinalIgnoreCase)) u.Dependencies.Add("VehFuncs V");
        if (corpus.Contains("openiv", StringComparison.OrdinalIgnoreCase)) u.Notes.Add("La notice du mod mentionne OpenIV (nécessaire pour l'étape dlclist.xml).");

        foreach (var ini in Directory.EnumerateFiles(dir, "*.ini", SearchOption.AllDirectories))
            if (ReadText(ini).Contains("[VehicleSetup]", StringComparison.OrdinalIgnoreCase))
            {
                u.VehFuncsIni.Add(Path.GetRelativePath(dir, ini));
                if (!u.Dependencies.Contains("VehFuncs V")) u.Dependencies.Add("VehFuncs V");
            }
        if (u.Dependencies.Contains("VehFuncs V"))
            u.Notes.Add("VehFuncs V (animations : rétroviseurs, essuie-glaces…) nécessite en général Script Hook V.");

        var mentioned = Regex.Matches(corpus, @"dlcpacks:\\+([A-Za-z0-9_\-]+)").Select(m => m.Groups[1].Value).Distinct().ToList();
        if (mentioned.Count > 0 && !mentioned.Contains(u.Name, StringComparer.OrdinalIgnoreCase))
            u.Warnings.Add("La notice cite « " + string.Join(", ", mentioned) + " » pour la ligne dlclist ; pour cette variante, la ligne correcte utilise le nom du dossier : « " + u.Name + " ».");
        return u;
    }

    static ModUnit BuildReplaceUnit(string dir)
    {
        var u = new ModUnit { Kind = ModKind.ReplaceVehicle, SourceDir = dir };
        var name = new DirectoryInfo(dir).Name;
        if (name.Equals("replace", StringComparison.OrdinalIgnoreCase) || name.Equals("files", StringComparison.OrdinalIgnoreCase))
            name = new DirectoryInfo(dir).Parent?.Name ?? name;
        u.Name = name;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            u.Files.Add(Path.GetRelativePath(dir, f));

        string Stem(string f) { var s = Path.GetFileNameWithoutExtension(f).ToLowerInvariant(); return s.EndsWith("_hi") ? s[..^3] : s; }
        var yft = u.Files.Where(f => f.EndsWith(".yft", StringComparison.OrdinalIgnoreCase)).Select(Stem).Distinct().ToList();
        var ytd = u.Files.Where(f => f.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase)).Select(Stem).Distinct().ToList();
        u.VehicleModel = yft.FirstOrDefault() ?? ytd.FirstOrDefault();
        u.Notes.Add("Mod de REMPLACEMENT : ces fichiers remplacent ceux d'un véhicule existant du jeu (« " + u.VehicleModel + " »).");
        return u;
    }
}
