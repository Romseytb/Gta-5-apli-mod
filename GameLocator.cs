using Microsoft.Win32;

namespace GtaModsManager.Core;

public static class GameLocator
{
    public const string LegacyExe = "GTA5.exe";
    public const string EnhancedExe = "GTA5_Enhanced.exe";

    static readonly string[] RelativeCandidates =
    {
        @"Games\Rockstar Games\GTA 5",
        @"Games\Rockstar Games\Grand Theft Auto V",
        @"Games\Grand Theft Auto V",
        @"Games\GTA 5",
        @"Games\GTAV",
        @"Rockstar Games\Grand Theft Auto V",
        @"Rockstar Games\GTA 5",
        @"Program Files\Rockstar Games\Grand Theft Auto V",
        @"Program Files (x86)\Rockstar Games\Grand Theft Auto V",
        @"Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
        @"Program Files\Epic Games\GTAV",
        @"SteamLibrary\steamapps\common\Grand Theft Auto V",
    };

    public static bool IsGameDir(string dir)
        => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)
           && (File.Exists(Path.Combine(dir, LegacyExe)) || File.Exists(Path.Combine(dir, EnhancedExe)));

    /// <summary>Détection prudente : Enhanced si GTA5_Enhanced.exe ; Legacy si GTA5.exe + x64a.rpf ; sinon inconnue.</summary>
    public static GameVariant DetectVariant(string dir)
    {
        if (!IsGameDir(dir)) return GameVariant.Unknown;
        if (File.Exists(Path.Combine(dir, EnhancedExe))) return GameVariant.Enhanced;
        if (File.Exists(Path.Combine(dir, LegacyExe)) && File.Exists(Path.Combine(dir, "x64a.rpf"))) return GameVariant.Legacy;
        return GameVariant.Unknown;
    }

    public static List<string> FindCandidates(IEnumerable<string> roots, IEnumerable<string> hints = null)
    {
        var found = new List<string>();
        void Add(string d)
        {
            if (IsGameDir(d) && !found.Contains(d, StringComparer.OrdinalIgnoreCase)) found.Add(d);
        }
        if (hints != null) foreach (var h in hints) Add(h);
        foreach (var root in roots)
        {
            foreach (var rel in RelativeCandidates) Add(Path.Combine(root, rel));
            foreach (var parent in new[] { Path.Combine(root, "Games"), Path.Combine(root, "Games", "Rockstar Games"), Path.Combine(root, "Rockstar Games") })
            {
                if (!Directory.Exists(parent)) continue;
                try { foreach (var sub in Directory.EnumerateDirectories(parent)) Add(sub); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return found;
    }

    public static IEnumerable<string> DefaultRoots()
        => DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);

    public static IEnumerable<string> RegistryHints()
    {
        var res = new List<string>();
        if (!OperatingSystem.IsWindows()) return res;
        string[] keys = { @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", @"SOFTWARE\Rockstar Games\Grand Theft Auto V" };
        foreach (var k in keys)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(k);
                var v = key?.GetValue("InstallFolder") as string;
                if (!string.IsNullOrEmpty(v)) res.Add(v);
            }
            catch { }
        }
        try
        {
            using var s = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var p = s?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(p)) res.Add(Path.Combine(p, "steamapps", "common", "Grand Theft Auto V"));
        }
        catch { }
        return res;
    }
}
