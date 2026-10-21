using System.IO.Compression;

namespace GtaModsManager.Core;

public static class SafeZip
{
    const long MaxTotalBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>Extrait un ZIP en refusant toute entrée qui sortirait du dossier de destination (zip slip).</summary>
    public static void Extract(string zipPath, string destDir)
    {
        Directory.CreateDirectory(destDir);
        using var zip = ZipFile.OpenRead(zipPath);
        long total = 0;
        foreach (var e in zip.Entries)
        {
            total += e.Length;
            if (total > MaxTotalBytes) throw new InvalidDataException("Archive trop volumineuse (plus de 8 Go décompressés).");
            if (string.IsNullOrEmpty(e.Name)) continue; // dossier
            var target = Path.GetFullPath(Path.Combine(destDir, e.FullName));
            if (!PathGuard.IsInside(destDir, target))
                throw new InvalidDataException("Archive refusée : elle contient un chemin dangereux (" + e.FullName + ").");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, true);
        }
    }
}
