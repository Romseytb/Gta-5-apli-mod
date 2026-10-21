using System.IO.Compression;
using System.Text;
using GtaModsManager.Core;

namespace GtaModsManager.Tests;

public static class TestHelpers
{
    public static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "gmm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    public static string FakeGame(string root, bool enhanced = false, bool legacyMarker = true)
    {
        var g = Path.Combine(root, "Games", "Rockstar Games", "GTA 5");
        Directory.CreateDirectory(Path.Combine(g, "update"));
        File.WriteAllText(Path.Combine(g, enhanced ? "GTA5_Enhanced.exe" : "GTA5.exe"), "exe-original");
        if (legacyMarker && !enhanced) File.WriteAllText(Path.Combine(g, "x64a.rpf"), "rpf-original");
        File.WriteAllText(Path.Combine(g, "update", "update.rpf"), "update-original");
        return g;
    }

    public static byte[] FakeRpf()
    {
        var b = new byte[64];
        BitConverter.GetBytes(0x52504637u).CopyTo(b, 0);
        BitConverter.GetBytes(28u).CopyTo(b, 4);
        BitConverter.GetBytes(368u).CopyTo(b, 8);
        BitConverter.GetBytes(0x4E45504Fu).CopyTo(b, 12);
        return b;
    }

    /// <summary>Reproduit la structure du ZIP réel fourni (2 mods : BearCat GIGN + Aventador SVJ 63).</summary>
    public static string MakeSampleZip(string dir)
    {
        var stage = Path.Combine(dir, "stage");
        string W(string rel, byte[] data) { var p = Path.Combine(stage, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllBytes(p, data); return p; }
        string T(string rel, string text) => W(rel, Encoding.UTF8.GetBytes(text));

        var ini = "[VehicleSetup]\r\nPartsAmount=10\r\n\r\n[1]\r\nName=retrovisor_l\r\n";
        var notice = "Install: copy emc_svj63 to dlcpacks. Add <Item>dlcpacks:\\emc_svj63\\</Item> with OpenIV. Install Vehfuncs V.";
        T(@"blinde\bearcat gign by Vixouu\Conditions d'utlisation.txt", "Merci de rejoindre mon discord");
        W(@"blinde\bearcat gign by Vixouu\replace\pbus.yft", new byte[] { 1, 2, 3 });
        W(@"blinde\bearcat gign by Vixouu\replace\pbus_hi.yft", new byte[] { 1, 2, 3 });
        W(@"blinde\bearcat gign by Vixouu\replace\pbus.ytd", new byte[] { 4, 5 });
        W(@"svj\emc_svj63\dlc.rpf", FakeRpf());
        T(@"svj\emc_svj63\emc_svj63.ini", ini);
        W(@"svj\emc_svj63b\dlc.rpf", FakeRpf());
        T(@"svj\emc_svj63b\emc_svj63b.ini", ini);
        T(@"svj\notice.txt", notice);

        var zip = Path.Combine(dir, "mods.zip");
        ZipFile.CreateFromDirectory(stage, zip);
        return zip;
    }

    public static (Installer inst, HistoryStore hist, AppPaths paths) NewInstaller(string dataRoot, bool running = false)
    {
        var paths = new AppPaths(Path.Combine(dataRoot, "data"));
        var hist = new HistoryStore(paths);
        var inst = new Installer(paths, hist) { IsGameRunning = () => running };
        return (inst, hist, paths);
    }

    public static ModUnit DlcUnit(string root, string name = "emc_svj63", bool withIni = true)
    {
        var src = Path.Combine(root, "src", name);
        Directory.CreateDirectory(src);
        File.WriteAllBytes(Path.Combine(src, "dlc.rpf"), FakeRpf());
        var u = new ModUnit { Kind = ModKind.AddOnDlc, Name = name, SourceDir = src };
        u.Files.Add("dlc.rpf");
        if (withIni)
        {
            File.WriteAllText(Path.Combine(src, name + ".ini"), "[VehicleSetup]");
            u.Files.Add(name + ".ini");
            u.VehFuncsIni.Add(name + ".ini");
            u.Dependencies.Add("VehFuncs V");
        }
        return u;
    }
}
