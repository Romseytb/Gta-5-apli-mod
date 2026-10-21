using System.Text;

namespace GtaModsManager.Core;

public static class Report
{
    public static string VariantLabel(GameVariant v) => v switch
    {
        GameVariant.Legacy => "GTA V Legacy",
        GameVariant.Enhanced => "GTA V Enhanced (non pris en charge)",
        _ => "version non déterminée"
    };

    public static string Describe(ModUnit u, InstallPlan plan, GameVariant variant)
    {
        var sb = new StringBuilder();
        sb.AppendLine(u.Name);
        sb.AppendLine(new string('─', Math.Min(60, Math.Max(10, u.Name.Length))));
        if (u.Kind == ModKind.AddOnDlc)
        {
            sb.AppendLine("Type : véhicule AJOUTÉ (pack DLC). Il s'ajoute au jeu sans remplacer de véhicule existant.");
            sb.AppendLine("Installation automatique : OUI pour la copie des fichiers, MAIS une étape manuelle dans OpenIV reste obligatoire (dlclist.xml).");
        }
        else if (u.Kind == ModKind.ReplaceVehicle)
        {
            sb.AppendLine("Type : véhicule de REMPLACEMENT (remplace le véhicule « " + u.VehicleModel + " »).");
            var target = ModAnalyzer.KnownTarget(u.VehicleModel);
            sb.AppendLine(target != null
                ? "Emplacement probable dans le jeu (à confirmer dans OpenIV) : " + target
                : "Emplacement dans le jeu : je ne le connais pas avec certitude, je ne ferai rien sans confirmation.");
            sb.AppendLine("Installation automatique : NON. Cela demande de modifier une archive .rpf du jeu ; je prépare les fichiers et je vous guide.");
        }
        sb.AppendLine();
        sb.AppendLine("Fichiers (" + u.Files.Count + ") : " + string.Join(", ", u.Files.Take(8)) + (u.Files.Count > 8 ? ", …" : ""));
        if (u.Dependencies.Count > 0) { sb.AppendLine(); sb.AppendLine("Dépendances : " + string.Join(", ", u.Dependencies)); }
        foreach (var n in u.Notes) sb.AppendLine("• " + n);
        foreach (var w in u.Warnings) sb.AppendLine("⚠ " + w);
        if (plan != null)
        {
            foreach (var w in plan.Warnings) sb.AppendLine("⚠ " + w);
            if (plan.Files.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Ce qui sera ajouté dans le dossier du jeu :");
                foreach (var f in plan.Files)
                    sb.AppendLine("  + " + f.Destination + (f.Exists ? "   ← EXISTE DÉJÀ (sauvegardé avant remplacement)" : ""));
            }
            foreach (var p in plan.Problems) sb.AppendLine("✖ " + p);
        }
        return sb.ToString();
    }
}
