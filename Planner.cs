namespace GtaModsManager.Core;

public static class Planner
{
    static readonly string[] SkippedExt = { ".ini", ".txt", ".md", ".nfo", ".png", ".jpg", ".jpeg" };

    /// <summary>Construit le plan d'installation SANS rien écrire.</summary>
    public static InstallPlan Build(ModUnit unit, string gameDir, GameVariant variant)
    {
        var plan = new InstallPlan { Unit = unit };
        if (!GameLocator.IsGameDir(gameDir))
        {
            plan.Problems.Add("Le dossier de GTA V n'est pas valide (GTA5.exe introuvable).");
            return plan;
        }
        if (variant == GameVariant.Enhanced)
            plan.Problems.Add("Votre jeu semble être GTA V Enhanced. Ces méthodes de mods (OpenIV, Script Hook V) sont prévues pour la version Legacy : je refuse d'installer pour ne pas risquer de casser le jeu.");
        if (variant == GameVariant.Unknown)
            plan.Warnings.Add("Je n'ai pas pu déterminer avec certitude si votre jeu est Legacy ou Enhanced.");
        if (!unit.CanAutoInstall)
        {
            plan.Problems.Add("Ce type de mod ne peut pas être installé automatiquement : suivez les explications affichées.");
            return plan;
        }
        if (unit.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || unit.Name.Contains(".."))
        {
            plan.Problems.Add("Le nom du mod contient des caractères non valides.");
            return plan;
        }

        var dlcRoot = Path.Combine(gameDir, "mods", "update", "x64", "dlcpacks", unit.Name);
        void Add(string src, string dest)
        {
            if (!PathGuard.IsAllowedDestination(gameDir, dest))
            {
                plan.Problems.Add("Destination refusée (hors des dossiers autorisés) : " + dest);
                return;
            }
            plan.Files.Add(new PlannedFile { Source = src, Destination = dest, Exists = File.Exists(dest) });
        }
        foreach (var rel in unit.Files)
        {
            if (SkippedExt.Contains(Path.GetExtension(rel).ToLowerInvariant())) continue;
            Add(Path.Combine(unit.SourceDir, rel), Path.Combine(dlcRoot, rel));
        }
        foreach (var ini in unit.VehFuncsIni)
            Add(Path.Combine(unit.SourceDir, ini), Path.Combine(gameDir, "VehFuncsV", Path.GetFileName(ini)));
        if (plan.Files.Count == 0) plan.Problems.Add("Aucun fichier à installer pour ce mod.");

        if (!File.Exists(Path.Combine(gameDir, "OpenIV.asi")))
            plan.Warnings.Add("OpenIV (fichier OpenIV.asi) n'est pas détecté dans le dossier du jeu : sans lui, le jeu ne lira pas le dossier « mods ».");
        if (unit.Dependencies.Contains("VehFuncs V"))
        {
            if (!Directory.Exists(Path.Combine(gameDir, "VehFuncsV")) && !File.Exists(Path.Combine(gameDir, "VehFuncsV.asi")))
                plan.Warnings.Add("Dépendance : VehFuncs V ne semble pas installé. Le véhicule peut apparaître, mais ses animations (rétroviseurs, essuie-glaces, etc.) ne fonctionneront pas sans lui.");
            if (!File.Exists(Path.Combine(gameDir, "ScriptHookV.dll")))
                plan.Warnings.Add("Script Hook V (ScriptHookV.dll) n'est pas détecté ; VehFuncs V en a généralement besoin.");
        }

        plan.ManualSteps.Add("Étape manuelle obligatoire (je ne peux pas la faire à votre place, update.rpf est chiffré) : ouvrez OpenIV en mode édition, allez dans mods > update > update.rpf > common > data > dlclist.xml. OpenIV travaille sur une copie dans « mods » : l'original n'est pas touché.");
        plan.ManualSteps.Add("Ajoutez cette ligne avec les autres lignes <Item>dlcpacks:… :   " + unit.DlcListLine + "   (elle a été copiée dans le presse-papiers).");
        plan.ManualSteps.Add("Enregistrez dans OpenIV, puis lancez GTA V comme d'habitude (Mephisto). Le nom pour faire apparaître le véhicule se trouve dans la notice du mod.");
        return plan;
    }
}
