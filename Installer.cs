using System.Diagnostics;
using System.Security.Cryptography;

namespace GtaModsManager.Core;

public class Installer
{
    readonly AppPaths paths;
    readonly HistoryStore history;

    public Func<bool> IsGameRunning { get; set; } = DefaultIsGameRunning;

    public Installer(AppPaths paths, HistoryStore history) { this.paths = paths; this.history = history; }

    static bool DefaultIsGameRunning()
        => Process.GetProcessesByName("GTA5").Length > 0 || Process.GetProcessesByName("GTA5_Enhanced").Length > 0;

    public static string Hash(string file)
    {
        using var fs = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(fs));
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    static void EnsureDir(string dir, List<string> created)
    {
        if (Directory.Exists(dir)) return;
        var parent = Path.GetDirectoryName(dir);
        if (!string.IsNullOrEmpty(parent)) EnsureDir(parent, created);
        Directory.CreateDirectory(dir);
        created.Add(dir);
    }

    public InstallRecord Install(InstallPlan plan, string gameDir, bool allowOverwrite)
    {
        if (plan.Problems.Count > 0)
            throw new InvalidOperationException("Installation impossible : " + string.Join(" ", plan.Problems));
        if (IsGameRunning())
            throw new InvalidOperationException("GTA V est en cours d'exécution. Fermez le jeu puis recommencez.");
        foreach (var f in plan.Files)
            if (!PathGuard.IsAllowedDestination(gameDir, f.Destination))
                throw new InvalidOperationException("Destination refusée : " + f.Destination);

        var conflicts = plan.Files.Where(f => File.Exists(f.Destination)).ToList();
        if (conflicts.Count > 0 && !allowOverwrite)
            throw new ConflictException("Des fichiers existent déjà (aucun n'a été modifié) : " + string.Join(", ", conflicts.Select(c => Path.GetFileName(c.Destination))));

        var id = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Sanitize(plan.Unit.Name);
        var backupDir = Path.Combine(paths.BackupDir, id);
        Directory.CreateDirectory(backupDir);
        var record = new InstallRecord
        {
            Id = id, ModName = plan.Unit.Name, Kind = plan.Unit.Kind.ToString(),
            GamePath = gameDir, InstalledAt = DateTime.Now, ManualSteps = plan.ManualSteps.ToList()
        };
        try
        {
            int i = 0;
            foreach (var f in plan.Files)
            {
                i++;
                var entry = new FileEntry { Path = f.Destination };
                if (File.Exists(f.Destination))
                {
                    var b = Path.Combine(backupDir, i + ".bak");
                    File.Copy(f.Destination, b, false);
                    if (Hash(b) != Hash(f.Destination)) throw new IOException("La sauvegarde de " + Path.GetFileName(f.Destination) + " est incorrecte.");
                    entry.Action = "Overwritten";
                    entry.BackupFile = b;
                }
                record.Entries.Add(entry); // ajouté AVANT la copie pour que l'annulation nettoie même une copie partielle
                EnsureDir(Path.GetDirectoryName(f.Destination)!, record.CreatedDirs);
                var srcHash = Hash(f.Source);
                File.Copy(f.Source, f.Destination, true);
                entry.Sha256 = Hash(f.Destination);
                if (entry.Sha256 != srcHash) throw new IOException("La copie de " + Path.GetFileName(f.Destination) + " est incorrecte.");
            }
            history.Save(record);
        }
        catch
        {
            Rollback(record);
            throw;
        }
        return record;
    }

    void Rollback(InstallRecord record)
    {
        foreach (var e in Enumerable.Reverse(record.Entries))
        {
            try
            {
                if (e.Action == "Overwritten" && e.BackupFile != null && File.Exists(e.BackupFile)) File.Copy(e.BackupFile, e.Path, true);
                else if (File.Exists(e.Path)) File.Delete(e.Path);
            }
            catch { }
        }
        RemoveEmptyDirs(record);
    }

    static void RemoveEmptyDirs(InstallRecord record)
    {
        foreach (var d in Enumerable.Reverse(record.CreatedDirs))
        {
            try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); }
            catch { }
        }
    }

    public UninstallResult Uninstall(InstallRecord record, bool force = false)
    {
        var res = new UninstallResult();
        if (record.Status != InstallStatus.Installed) throw new InvalidOperationException("Ce mod n'est pas installé.");
        if (IsGameRunning()) throw new InvalidOperationException("GTA V est en cours d'exécution. Fermez le jeu puis recommencez.");

        foreach (var e in Enumerable.Reverse(record.Entries))
        {
            if (!PathGuard.IsAllowedDestination(record.GamePath, e.Path))
            {
                res.Messages.Add("Ignoré (hors zone autorisée) : " + e.Path); res.Skipped++; continue;
            }
            if (!force && File.Exists(e.Path) && !string.IsNullOrEmpty(e.Sha256) && Hash(e.Path) != e.Sha256)
            {
                res.Messages.Add("Conservé (modifié depuis l'installation) : " + Path.GetFileName(e.Path)); res.Skipped++; continue;
            }
            if (e.Action == "Overwritten")
            {
                if (e.BackupFile != null && File.Exists(e.BackupFile)) { File.Copy(e.BackupFile, e.Path, true); res.Messages.Add("Restauré : " + Path.GetFileName(e.Path)); }
                else { res.Messages.Add("Sauvegarde introuvable : " + Path.GetFileName(e.Path)); res.Skipped++; }
            }
            else if (File.Exists(e.Path)) { File.Delete(e.Path); res.Messages.Add("Supprimé : " + Path.GetFileName(e.Path)); }
        }
        RemoveEmptyDirs(record);
        if (res.Skipped == 0)
        {
            record.Status = InstallStatus.Uninstalled;
            history.Save(record);
        }
        return res;
    }
}
