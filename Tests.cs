using System.IO.Compression;
using GtaModsManager.Core;
using Xunit;

namespace GtaModsManager.Tests;

public class AnalyzerTests
{
    [Fact]
    public void Analyse_zip_reconnait_les_deux_types_de_mods()
    {
        var d = TestHelpers.TempDir();
        var pkg = ModAnalyzer.Analyze(TestHelpers.MakeSampleZip(d), Path.Combine(d, "work"));

        Assert.Equal(3, pkg.Units.Count);
        var dlc = pkg.Units.Where(u => u.Kind == ModKind.AddOnDlc).ToList();
        Assert.Equal(2, dlc.Count);
        Assert.All(dlc, u => Assert.Contains("VehFuncs V", u.Dependencies));
        Assert.All(dlc, u => Assert.Single(u.VehFuncsIni));

        var rep = Assert.Single(pkg.Units, u => u.Kind == ModKind.ReplaceVehicle);
        Assert.Equal("pbus", rep.VehicleModel);
        Assert.False(rep.CanAutoInstall);
        Assert.Equal(3, rep.Files.Count);
    }

    [Fact]
    public void Analyse_signale_les_variantes_et_la_ligne_dlclist_de_la_variante_b()
    {
        var d = TestHelpers.TempDir();
        var pkg = ModAnalyzer.Analyze(TestHelpers.MakeSampleZip(d), Path.Combine(d, "work"));
        var b = pkg.Units.Single(u => u.Name == "emc_svj63b");
        Assert.Contains(b.Warnings, w => w.Contains("variantes"));
        Assert.Contains(b.Warnings, w => w.Contains("emc_svj63b"));
        Assert.Equal("<Item>dlcpacks:\\emc_svj63b\\</Item>", b.DlcListLine);
    }

    [Fact]
    public void Zip_dangereux_est_refuse()
    {
        var d = TestHelpers.TempDir();
        var zip = Path.Combine(d, "evil.zip");
        using (var fs = File.Create(zip))
        using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var e = za.CreateEntry("../evil.txt");
            using var w = new StreamWriter(e.Open()); w.Write("x");
        }
        Assert.Throws<InvalidDataException>(() => ModAnalyzer.Analyze(zip, Path.Combine(d, "work")));
        Assert.False(File.Exists(Path.Combine(d, "evil.txt")));
    }

    [Fact]
    public void Format_non_pris_en_charge_donne_un_message_clair()
    {
        var d = TestHelpers.TempDir();
        var rar = Path.Combine(d, "mod.rar"); File.WriteAllText(rar, "x");
        var pkg = ModAnalyzer.Analyze(rar, Path.Combine(d, "work"));
        Assert.Empty(pkg.Units);
        Assert.Contains(pkg.Messages, m => m.Contains(".rar"));
    }

    [Fact]
    public void Rpf_invalide_declenche_un_avertissement()
    {
        var d = TestHelpers.TempDir();
        var dir = Path.Combine(d, "mod", "veh"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "dlc.rpf"), "pas un rpf du tout, juste du texte");
        var pkg = ModAnalyzer.Analyze(Path.Combine(d, "mod"), Path.Combine(d, "work"));
        Assert.Contains(pkg.Units.Single().Warnings, w => w.Contains("RPF7"));
    }
}

public class GameLocatorTests
{
    [Fact]
    public void Trouve_le_jeu_dans_C_Games_Rockstar_Games_GTA_5()
    {
        var root = TestHelpers.TempDir();
        var g = TestHelpers.FakeGame(root);
        var found = GameLocator.FindCandidates(new[] { root });
        Assert.Contains(found, f => string.Equals(f, g, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Detecte_legacy_enhanced_et_inconnu()
    {
        Assert.Equal(GameVariant.Legacy, GameLocator.DetectVariant(TestHelpers.FakeGame(TestHelpers.TempDir())));
        Assert.Equal(GameVariant.Enhanced, GameLocator.DetectVariant(TestHelpers.FakeGame(TestHelpers.TempDir(), enhanced: true)));
        Assert.Equal(GameVariant.Unknown, GameLocator.DetectVariant(TestHelpers.FakeGame(TestHelpers.TempDir(), legacyMarker: false)));
        Assert.Equal(GameVariant.Unknown, GameLocator.DetectVariant(TestHelpers.TempDir()));
    }

    [Fact]
    public void Dossier_sans_exe_nest_pas_un_dossier_de_jeu()
        => Assert.False(GameLocator.IsGameDir(TestHelpers.TempDir()));
}

public class PathGuardTests
{
    [Theory]
    [InlineData(@"GTA5.exe", false)]
    [InlineData(@"update\update.rpf", false)]
    [InlineData(@"x64a.rpf", false)]
    [InlineData(@"..\autre\fichier.txt", false)]
    [InlineData(@"mods\update\x64\dlcpacks\emc\dlc.rpf", true)]
    [InlineData(@"VehFuncsV\emc.ini", true)]
    public void Seules_les_zones_autorisees_sont_acceptees(string rel, bool expected)
    {
        var g = TestHelpers.FakeGame(TestHelpers.TempDir());
        var dest = Path.Combine(g, rel.Replace('\\', Path.DirectorySeparatorChar));
        Assert.Equal(expected, PathGuard.IsAllowedDestination(g, dest));
    }
}

public class PlannerTests
{
    [Fact]
    public void Plan_ne_cible_que_mods_et_VehFuncsV()
    {
        var root = TestHelpers.TempDir();
        var g = TestHelpers.FakeGame(root);
        var plan = Planner.Build(TestHelpers.DlcUnit(root), g, GameVariant.Legacy);
        Assert.Empty(plan.Problems);
        Assert.Equal(2, plan.Files.Count);
        Assert.All(plan.Files, f => Assert.True(PathGuard.IsAllowedDestination(g, f.Destination)));
        Assert.Contains(plan.Files, f => f.Destination.EndsWith(Path.Combine("dlcpacks", "emc_svj63", "dlc.rpf")));
        Assert.Contains(plan.Files, f => f.Destination.EndsWith(Path.Combine("VehFuncsV", "emc_svj63.ini")));
        Assert.Contains(plan.Warnings, w => w.Contains("OpenIV"));
        Assert.Contains(plan.Warnings, w => w.Contains("VehFuncs V"));
        Assert.Contains(plan.ManualSteps, s => s.Contains("dlcpacks:\\emc_svj63\\"));
    }

    [Fact]
    public void Enhanced_est_refuse()
    {
        var root = TestHelpers.TempDir();
        var g = TestHelpers.FakeGame(root, enhanced: true);
        var plan = Planner.Build(TestHelpers.DlcUnit(root), g, GameVariant.Enhanced);
        Assert.Contains(plan.Problems, p => p.Contains("Enhanced"));
    }

    [Fact]
    public void Mod_de_remplacement_nest_jamais_installe_automatiquement()
    {
        var root = TestHelpers.TempDir();
        var g = TestHelpers.FakeGame(root);
        var u = new ModUnit { Kind = ModKind.ReplaceVehicle, Name = "bearcat", VehicleModel = "pbus" };
        var plan = Planner.Build(u, g, GameVariant.Legacy);
        Assert.NotEmpty(plan.Problems);
        Assert.Empty(plan.Files);
    }

    [Fact]
    public void Dossier_de_jeu_invalide_est_refuse()
    {
        var plan = Planner.Build(TestHelpers.DlcUnit(TestHelpers.TempDir()), TestHelpers.TempDir(), GameVariant.Unknown);
        Assert.NotEmpty(plan.Problems);
    }

    [Fact]
    public void Plan_ne_cree_aucun_fichier()
    {
        var root = TestHelpers.TempDir();
        var g = TestHelpers.FakeGame(root);
        Planner.Build(TestHelpers.DlcUnit(root), g, GameVariant.Legacy);
        Assert.False(Directory.Exists(Path.Combine(g, "mods")));
    }
}

public class InstallerTests
{
    static (string root, string game, ModUnit unit, Installer inst, AppPaths paths) Setup(bool running = false)
    {
        var root = TestHelpers.TempDir();
        var game = TestHelpers.FakeGame(root);
        var unit = TestHelpers.DlcUnit(root);
        var (inst, _, paths) = TestHelpers.NewInstaller(root, running);
        return (root, game, unit, inst, paths);
    }

    [Fact]
    public void Installation_copie_les_fichiers_sans_toucher_aux_fichiers_du_jeu()
    {
        var (root, g, unit, inst, _) = Setup();
        var before = new[] { "GTA5.exe", "x64a.rpf", Path.Combine("update", "update.rpf") }.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(g, f)));
        var rec = inst.Install(Planner.Build(unit, g, GameVariant.Legacy), g, false);

        Assert.True(File.Exists(Path.Combine(g, "mods", "update", "x64", "dlcpacks", "emc_svj63", "dlc.rpf")));
        Assert.True(File.Exists(Path.Combine(g, "VehFuncsV", "emc_svj63.ini")));
        foreach (var kv in before) Assert.Equal(kv.Value, File.ReadAllText(Path.Combine(g, kv.Key)));
        Assert.Equal(InstallStatus.Installed, rec.Status);
    }

    [Fact]
    public void Aucun_ecrasement_silencieux()
    {
        var (root, g, unit, inst, _) = Setup();
        var existing = Path.Combine(g, "VehFuncsV", "emc_svj63.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "ORIGINAL");
        var plan = Planner.Build(unit, g, GameVariant.Legacy);
        Assert.True(plan.HasConflicts);

        Assert.Throws<ConflictException>(() => inst.Install(plan, g, false));
        Assert.Equal("ORIGINAL", File.ReadAllText(existing));
        Assert.False(File.Exists(Path.Combine(g, "mods", "update", "x64", "dlcpacks", "emc_svj63", "dlc.rpf")));
    }

    [Fact]
    public void Sauvegarde_puis_desinstallation_restaure_letat_initial()
    {
        var (root, g, unit, inst, paths) = Setup();
        var existing = Path.Combine(g, "VehFuncsV", "emc_svj63.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "ORIGINAL");

        var rec = inst.Install(Planner.Build(unit, g, GameVariant.Legacy), g, true);
        Assert.Equal("[VehicleSetup]", File.ReadAllText(existing));
        var entry = rec.Entries.Single(e => e.Action == "Overwritten");
        Assert.Equal("ORIGINAL", File.ReadAllText(entry.BackupFile));

        var res = inst.Uninstall(rec);
        Assert.Equal(0, res.Skipped);
        Assert.Equal("ORIGINAL", File.ReadAllText(existing));
        Assert.False(File.Exists(Path.Combine(g, "mods", "update", "x64", "dlcpacks", "emc_svj63", "dlc.rpf")));
        Assert.False(Directory.Exists(Path.Combine(g, "mods")));
        Assert.Equal(InstallStatus.Uninstalled, rec.Status);
    }

    [Fact]
    public void Historique_est_conserve_sur_disque()
    {
        var (root, g, unit, inst, paths) = Setup();
        inst.Install(Planner.Build(unit, g, GameVariant.Legacy), g, false);
        var all = new HistoryStore(paths).LoadAll();
        Assert.Single(all);
        Assert.Equal("emc_svj63", all[0].ModName);
    }

    [Fact]
    public void Installation_refusee_si_le_jeu_tourne()
    {
        var (root, g, unit, inst, _) = Setup(running: true);
        Assert.Throws<InvalidOperationException>(() => inst.Install(Planner.Build(unit, g, GameVariant.Legacy), g, false));
        Assert.False(Directory.Exists(Path.Combine(g, "mods")));
    }

    [Fact]
    public void Echec_en_cours_de_route_annule_tout()
    {
        var (root, g, unit, inst, _) = Setup();
        var plan = Planner.Build(unit, g, GameVariant.Legacy);
        File.Delete(plan.Files.Last().Source); // le 2e fichier source disparaît
        Assert.ThrowsAny<Exception>(() => inst.Install(plan, g, false));
        Assert.False(File.Exists(Path.Combine(g, "mods", "update", "x64", "dlcpacks", "emc_svj63", "dlc.rpf")));
        Assert.False(Directory.Exists(Path.Combine(g, "mods")));
    }

    [Fact]
    public void Fichier_modifie_apres_installation_nest_pas_supprime_sans_accord()
    {
        var (root, g, unit, inst, _) = Setup();
        var rec = inst.Install(Planner.Build(unit, g, GameVariant.Legacy), g, false);
        var dlc = Path.Combine(g, "mods", "update", "x64", "dlcpacks", "emc_svj63", "dlc.rpf");
        File.AppendAllText(dlc, "modifié par l'utilisateur");

        var res = inst.Uninstall(rec);
        Assert.True(res.Skipped > 0);
        Assert.True(File.Exists(dlc));
        Assert.Equal(InstallStatus.Installed, rec.Status);

        inst.Uninstall(rec, force: true);
        Assert.False(File.Exists(dlc));
    }
}
