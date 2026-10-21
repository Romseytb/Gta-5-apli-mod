namespace GtaModsManager.Core;

public enum ModKind { AddOnDlc, ReplaceVehicle, Unknown }
public enum GameVariant { Unknown, Legacy, Enhanced }

public class ModUnit
{
    public string Name { get; set; } = "";
    public ModKind Kind { get; set; } = ModKind.Unknown;
    public string SourceDir { get; set; } = "";
    public List<string> Files { get; } = new();
    public List<string> VehFuncsIni { get; } = new();
    public List<string> Dependencies { get; } = new();
    public List<string> Notes { get; } = new();
    public List<string> Warnings { get; } = new();
    public string VehicleModel { get; set; }
    public string DlcListLine => "<Item>dlcpacks:\\" + Name + "\\</Item>";
    public bool CanAutoInstall => Kind == ModKind.AddOnDlc;
}

public class ModPackage
{
    public string Source { get; set; } = "";
    public string WorkDir { get; set; }
    public List<ModUnit> Units { get; } = new();
    public List<string> Messages { get; } = new();
}

public class PlannedFile
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Exists { get; set; }
}

public class InstallPlan
{
    public ModUnit Unit { get; set; }
    public List<PlannedFile> Files { get; } = new();
    public List<string> Problems { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> ManualSteps { get; } = new();
    public bool HasConflicts => Files.Any(f => f.Exists);
}

public class FileEntry
{
    public string Path { get; set; } = "";
    public string Action { get; set; } = "Created";   // Created | Overwritten
    public string BackupFile { get; set; }
    public string Sha256 { get; set; } = "";
}

public static class InstallStatus
{
    public const string Installed = "installed";
    public const string Uninstalled = "uninstalled";
}

public class InstallRecord
{
    public string Id { get; set; } = "";
    public string ModName { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateTime InstalledAt { get; set; } = DateTime.Now;
    public string GamePath { get; set; } = "";
    public string Status { get; set; } = InstallStatus.Installed;
    public List<FileEntry> Entries { get; set; } = new();
    public List<string> CreatedDirs { get; set; } = new();
    public List<string> ManualSteps { get; set; } = new();
}

public class UninstallResult
{
    public List<string> Messages { get; } = new();
    public int Skipped { get; set; }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
