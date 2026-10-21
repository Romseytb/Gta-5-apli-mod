namespace GtaModsManager.Core;

/// <summary>Garde-fous : l'application n'écrit que dans des zones autorisées du dossier du jeu.</summary>
public static class PathGuard
{
    static readonly string[] AllowedTopFolders = { "mods", "VehFuncsV", "scripts" };

    public static bool IsInside(string root, string path)
    {
        var r = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        var p = Path.GetFullPath(path);
        return p.StartsWith(r, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Vrai uniquement pour mods\, VehFuncsV\ et scripts\ : jamais GTA5.exe, update\, x64*.rpf, etc.</summary>
    public static bool IsAllowedDestination(string gameDir, string destination)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || string.IsNullOrWhiteSpace(destination)) return false;
        if (!IsInside(gameDir, destination)) return false;
        var rel = Path.GetRelativePath(gameDir, Path.GetFullPath(destination));
        var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return AllowedTopFolders.Contains(first, StringComparer.OrdinalIgnoreCase);
    }
}
