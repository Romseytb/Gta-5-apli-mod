using System.Text.Json;

namespace GtaModsManager.Core;

public class AppPaths
{
    public string DataDir { get; }
    public AppPaths(string dataDir = null)
    {
        DataDir = dataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GtaModsManager");
        Directory.CreateDirectory(DataDir);
    }
    public string ConfigFile => Path.Combine(DataDir, "config.json");
    public string HistoryDir => Path.Combine(DataDir, "historique");
    public string BackupDir => Path.Combine(DataDir, "sauvegardes");
    public string WorkDir => Path.Combine(DataDir, "travail");
    public string PreparedDir => Path.Combine(DataDir, "a-importer-dans-openiv");
}

public class AppConfig
{
    public string GamePath { get; set; }
    public bool GamePathConfirmed { get; set; }
}

public static class ConfigStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppConfig Load(AppPaths paths)
    {
        try
        {
            if (File.Exists(paths.ConfigFile))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(paths.ConfigFile)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppPaths paths, AppConfig cfg)
        => File.WriteAllText(paths.ConfigFile, JsonSerializer.Serialize(cfg, Opts));
}

public class HistoryStore
{
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    readonly AppPaths paths;
    public HistoryStore(AppPaths paths) { this.paths = paths; Directory.CreateDirectory(paths.HistoryDir); }

    public void Save(InstallRecord r)
        => File.WriteAllText(Path.Combine(paths.HistoryDir, r.Id + ".json"), JsonSerializer.Serialize(r, Opts));

    public List<InstallRecord> LoadAll()
    {
        var list = new List<InstallRecord>();
        foreach (var f in Directory.EnumerateFiles(paths.HistoryDir, "*.json"))
        {
            try
            {
                var r = JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(f));
                if (r != null) list.Add(r);
            }
            catch { }
        }
        return list.OrderByDescending(r => r.InstalledAt).ToList();
    }
}
