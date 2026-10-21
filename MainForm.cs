using System.Diagnostics;
using System.Text;
using GtaModsManager.Core;

namespace GtaModsManager;

public class MainForm : Form
{
    class UnitItem
    {
        public ModUnit Unit;
        public override string ToString() => (Unit.Kind == ModKind.AddOnDlc ? "[Ajout] " : "[Remplacement] ") + Unit.Name;
    }

    readonly AppPaths paths = new();
    readonly AppConfig config;
    readonly HistoryStore history;
    readonly Installer installer;

    string gameDir;
    GameVariant variant = GameVariant.Unknown;
    List<InstallRecord> records = new();

    readonly Label lblGame = new();
    readonly Button btnChangeGame = new();
    readonly Label lblDrop = new();
    readonly ListBox lstUnits = new();
    readonly RichTextBox rtbReport = new();
    readonly Button btnInstall = new();
    readonly ListBox lstHistory = new();
    readonly Button btnUninstall = new();
    readonly ToolStripStatusLabel statusLabel = new("Prêt");

    public MainForm()
    {
        Text = "GTA Mods Manager";
        Width = 980; Height = 720; MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        config = ConfigStore.Load(paths);
        history = new HistoryStore(paths);
        installer = new Installer(paths, history);
        BuildUi();
        Shown += async (s, e) => await InitGameAsync();
        FormClosed += (s, e) => { try { if (Directory.Exists(paths.WorkDir)) Directory.Delete(paths.WorkDir, true); } catch { } };
    }

    void BuildUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabInstall = new TabPage("Installer un mod");
        var tabHistory = new TabPage("Mods installés");
        var tabHelp = new TabPage("Aide");

        // ----- Onglet Installer
        lblDrop.Text = "Glissez-déposez ici un mod (ZIP ou dossier)\r\nou cliquez pour choisir un ZIP";
        lblDrop.Dock = DockStyle.Top; lblDrop.Height = 110;
        lblDrop.TextAlign = ContentAlignment.MiddleCenter;
        lblDrop.BorderStyle = BorderStyle.FixedSingle;
        lblDrop.BackColor = Color.FromArgb(235, 241, 250);
        lblDrop.Font = new Font("Segoe UI", 12F);
        lblDrop.Cursor = Cursors.Hand;
        lblDrop.AllowDrop = true;
        lblDrop.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        lblDrop.DragDrop += async (s, e) =>
        {
            var p = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (p != null && p.Length > 0) await AnalyzeAsync(p[0]);
        };
        lblDrop.Click += async (s, e) =>
        {
            using var dlg = new OpenFileDialog { Title = "Choisir un mod (ZIP)", Filter = "Archives ZIP (*.zip)|*.zip|Tous les fichiers|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) await AnalyzeAsync(dlg.FileName);
        };

        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 6, 0, 0) };
        var btnFolder = new Button { Text = "Choisir un dossier de mod…", AutoSize = true };
        btnFolder.Click += async (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Choisir le dossier du mod" };
            if (dlg.ShowDialog(this) == DialogResult.OK) await AnalyzeAsync(dlg.SelectedPath);
        };
        flow.Controls.Add(btnFolder);

        lstUnits.Dock = DockStyle.Left; lstUnits.Width = 270;
        lstUnits.SelectedIndexChanged += (s, e) => ShowReport();
        rtbReport.Dock = DockStyle.Fill; rtbReport.ReadOnly = true; rtbReport.BackColor = Color.White;
        rtbReport.Font = new Font("Segoe UI", 10F);
        rtbReport.Text = "Glissez un mod dans la zone ci-dessus : je l'analyse sans rien modifier.";

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(0, 8, 0, 0) };
        btnInstall.Text = "Installer ce mod"; btnInstall.Dock = DockStyle.Right; btnInstall.Width = 260;
        btnInstall.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnInstall.Enabled = false;
        btnInstall.Click += async (s, e) => await OnInstallAsync();
        bottom.Controls.Add(btnInstall);

        tabInstall.Controls.Add(rtbReport);
        tabInstall.Controls.Add(lstUnits);
        tabInstall.Controls.Add(bottom);
        tabInstall.Controls.Add(flow);
        tabInstall.Controls.Add(lblDrop);
        tabInstall.Padding = new Padding(10);

        // ----- Onglet Historique
        lstHistory.Dock = DockStyle.Fill;
        var bottom2 = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(0, 8, 0, 0) };
        btnUninstall.Text = "Désinstaller / restaurer la sélection"; btnUninstall.Dock = DockStyle.Right; btnUninstall.Width = 300;
        btnUninstall.Click += async (s, e) => await OnUninstallAsync();
        var btnBackups = new Button { Text = "Ouvrir le dossier des sauvegardes", Dock = DockStyle.Left, Width = 280 };
        btnBackups.Click += (s, e) => { Directory.CreateDirectory(paths.BackupDir); Process.Start("explorer.exe", paths.BackupDir); };
        bottom2.Controls.Add(btnUninstall);
        bottom2.Controls.Add(btnBackups);
        tabHistory.Controls.Add(lstHistory);
        tabHistory.Controls.Add(bottom2);
        tabHistory.Padding = new Padding(10);

        // ----- Onglet Aide
        var help = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Text = HelpText() };
        tabHelp.Controls.Add(help);
        tabHelp.Padding = new Padding(10);

        tabs.TabPages.Add(tabInstall);
        tabs.TabPages.Add(tabHistory);
        tabs.TabPages.Add(tabHelp);

        // ----- Bandeau du haut
        var top = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(12, 8, 12, 8), BackColor = Color.FromArgb(245, 245, 245) };
        lblGame.AutoSize = false; lblGame.Dock = DockStyle.Fill; lblGame.TextAlign = ContentAlignment.MiddleLeft;
        lblGame.Text = "Recherche de GTA V…";
        btnChangeGame.Text = "Changer le dossier du jeu…"; btnChangeGame.Dock = DockStyle.Right; btnChangeGame.Width = 230;
        btnChangeGame.Click += (s, e) => PickGameFolder();
        top.Controls.Add(lblGame);
        top.Controls.Add(btnChangeGame);

        var status = new StatusStrip();
        status.Items.Add(statusLabel);

        Controls.Add(tabs);
        Controls.Add(top);
        Controls.Add(status);
    }

    static string HelpText() =>
        "COMMENT ÇA MARCHE\r\n\r\n" +
        "1. Glissez un mod (ZIP ou dossier) dans la zone « Installer un mod ». Il est analysé sans rien modifier.\r\n" +
        "2. Lisez le résumé, puis cliquez sur « Installer ce mod ». Une fenêtre vous montre exactement ce qui sera copié et vous demande confirmation.\r\n" +
        "3. Pour un véhicule ajouté, une dernière étape dans OpenIV est obligatoire : le programme vous donne la ligne à copier.\r\n\r\n" +
        "SÉCURITÉ\r\n\r\n" +
        "• Le programme n'écrit que dans les dossiers « mods », « VehFuncsV » et « scripts » du jeu. Il ne modifie jamais GTA5.exe, update.rpf ni les .rpf d'origine.\r\n" +
        "• Avant toute écriture, les fichiers déjà présents sont sauvegardés. Rien n'est écrasé sans confirmation.\r\n" +
        "• L'onglet « Mods installés » permet de tout annuler : les fichiers ajoutés sont supprimés, les fichiers remplacés sont restaurés.\r\n" +
        "• Fermez GTA V avant d'installer ou de désinstaller.\r\n\r\n" +
        "LIMITES\r\n\r\n" +
        "• Les archives .rpf du jeu (update.rpf, vehicles.rpf…) sont chiffrées : leur modification passe par OpenIV. Les mods de remplacement et la ligne dlclist.xml demandent donc OpenIV.\r\n" +
        "• GTA V Enhanced n'est pas pris en charge (OpenIV / Script Hook V sont prévus pour Legacy).\r\n" +
        "• Votre lanceur (Mephisto) n'est pas touché. S'il vérifie ou répare les fichiers du jeu, il pourrait supprimer les mods : réinstallez-les alors depuis l'onglet « Installer ».\r\n";

    void SetStatus(string s) { statusLabel.Text = s; }

    // ---------- Jeu
    async Task InitGameAsync()
    {
        RefreshHistory();
        if (!string.IsNullOrEmpty(config.GamePath) && config.GamePathConfirmed && GameLocator.IsGameDir(config.GamePath))
        {
            SetGame(config.GamePath);
            return;
        }
        SetStatus("Recherche de GTA V…");
        var found = await Task.Run(() => GameLocator.FindCandidates(GameLocator.DefaultRoots(), GameLocator.RegistryHints()));
        foreach (var c in found)
        {
            var v = GameLocator.DetectVariant(c);
            var r = MessageBox.Show(this,
                "J'ai trouvé GTA V ici :\r\n\r\n" + c + "\r\n(" + Report.VariantLabel(v) + ")\r\n\r\nEst-ce le bon dossier ?\r\n\r\nAucun fichier du jeu n'est modifié pour l'instant.",
                "Dossier de GTA V", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) { ConfirmGame(c); return; }
        }
        PickGameFolder();
    }

    void PickGameFolder()
    {
        while (true)
        {
            using var dlg = new FolderBrowserDialog { Description = "Sélectionnez le dossier de GTA V (celui qui contient GTA5.exe)" };
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                if (gameDir == null) { lblGame.Text = "Dossier de GTA V non défini : cliquez sur « Changer le dossier du jeu… »."; SetStatus("Aucun jeu sélectionné"); }
                return;
            }
            if (GameLocator.IsGameDir(dlg.SelectedPath)) { ConfirmGame(dlg.SelectedPath); return; }
            MessageBox.Show(this, "GTA5.exe n'a pas été trouvé dans ce dossier. Choisissez le dossier qui contient GTA5.exe.", "Dossier incorrect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void ConfirmGame(string dir)
    {
        config.GamePath = dir; config.GamePathConfirmed = true;
        ConfigStore.Save(paths, config);
        SetGame(dir);
    }

    void SetGame(string dir)
    {
        gameDir = dir;
        variant = GameLocator.DetectVariant(dir);
        lblGame.Text = "Jeu : " + dir + "\r\nVersion : " + Report.VariantLabel(variant);
        SetStatus("Prêt");
        ShowReport();
    }

    // ---------- Analyse
    async Task AnalyzeAsync(string path)
    {
        SetStatus("Analyse en cours…");
        UseWaitCursor = true;
        ModPackage pkg;
        try { pkg = await Task.Run(() => ModAnalyzer.Analyze(path, paths.WorkDir)); }
        catch (Exception ex) { UseWaitCursor = false; ShowError("Impossible d'analyser ce mod", ex); SetStatus("Erreur d'analyse"); return; }
        UseWaitCursor = false;

        lstUnits.Items.Clear();
        foreach (var u in pkg.Units) lstUnits.Items.Add(new UnitItem { Unit = u });
        if (pkg.Units.Count == 0) { rtbReport.Text = string.Join("\r\n", pkg.Messages); btnInstall.Enabled = false; }
        else lstUnits.SelectedIndex = 0;
        SetStatus(pkg.Units.Count + " mod(s) reconnu(s). Aucun fichier du jeu n'a été modifié.");
    }

    ModUnit Selected => (lstUnits.SelectedItem as UnitItem)?.Unit;

    void ShowReport()
    {
        var u = Selected;
        if (u == null) { btnInstall.Enabled = false; return; }
        var plan = gameDir == null ? null : Planner.Build(u, gameDir, variant);
        rtbReport.Text = Report.Describe(u, plan, variant).Replace("\r\n", "\n").Replace("\n", "\r\n");
        btnInstall.Text = u.CanAutoInstall ? "Installer ce mod" : "Préparer pour OpenIV";
        btnInstall.Enabled = gameDir != null;
    }

    // ---------- Installation
    async Task OnInstallAsync()
    {
        try
        {
            var unit = Selected;
            if (unit == null || gameDir == null) return;
            if (!unit.CanAutoInstall) { PrepareManual(unit); return; }

            var plan = Planner.Build(unit, gameDir, variant);
            if (plan.Problems.Count > 0)
            {
                MessageBox.Show(this, string.Join("\r\n\r\n", plan.Problems), "Installation impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("Mod : " + unit.Name).AppendLine();
            sb.AppendLine("Fichiers qui vont être ajoutés dans le dossier du jeu :");
            foreach (var f in plan.Files)
                sb.AppendLine(" • " + Path.GetRelativePath(gameDir, f.Destination) + (f.Exists ? "   (EXISTE DÉJÀ : sauvegardé puis remplacé)" : ""));
            sb.AppendLine().AppendLine("Une sauvegarde est faite avant toute modification. Les fichiers d'origine du jeu ne sont pas touchés (seuls « mods » et « VehFuncsV » sont utilisés).");
            foreach (var w in plan.Warnings) sb.AppendLine().AppendLine("Attention : " + w);
            sb.AppendLine().Append("Continuer ?");
            var ans = MessageBox.Show(this, sb.ToString(), "Confirmer l'installation", MessageBoxButtons.YesNo,
                plan.HasConflicts ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
                plan.HasConflicts ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1);
            if (ans != DialogResult.Yes) return;

            SetStatus("Installation en cours…");
            btnInstall.Enabled = false; UseWaitCursor = true;
            InstallRecord rec;
            try { rec = await Task.Run(() => installer.Install(plan, gameDir, plan.HasConflicts)); }
            finally { UseWaitCursor = false; btnInstall.Enabled = true; }

            try { Clipboard.SetText(unit.DlcListLine); } catch { }
            RefreshHistory();
            SetStatus("Installé : " + unit.Name);
            MessageBox.Show(this,
                "Les fichiers ont été copiés et sauvegardés. Il reste une étape manuelle pour que le jeu voie le véhicule :\r\n\r\n" +
                string.Join("\r\n\r\n", rec.ManualSteps),
                "Installation terminée – une étape reste à faire", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { SetStatus("Échec : rien n'a été laissé à moitié"); ShowError("L'installation a échoué (les modifications ont été annulées)", ex); }
    }

    void PrepareManual(ModUnit unit)
    {
        var dest = Path.Combine(paths.PreparedDir, unit.Name);
        Directory.CreateDirectory(dest);
        foreach (var rel in unit.Files)
        {
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(unit.SourceDir, rel), target, true);
        }
        var tgt = ModAnalyzer.KnownTarget(unit.VehicleModel);
        Process.Start("explorer.exe", dest);
        MessageBox.Show(this,
            "Ce mod remplace le véhicule « " + unit.VehicleModel + " » du jeu. Je ne le fais pas automatiquement car il faut modifier une archive .rpf du jeu (chiffrée), ce que seul OpenIV sait faire sans risque.\r\n\r\n" +
            "J'ai copié les fichiers du mod ici (dossier ouvert) :\r\n" + dest + "\r\n\r\n" +
            "Dans OpenIV (mode édition activé) :\r\n" +
            (tgt != null ? "1. Ouvrez l'archive : " + tgt + " (emplacement probable, à vérifier).\r\n" : "1. Ouvrez l'archive qui contient le véhicule « " + unit.VehicleModel + " ».\r\n") +
            "2. Glissez les fichiers du dossier ouvert dans cette archive, en remplaçant les fichiers du même nom.\r\n" +
            "3. OpenIV travaille sur une copie dans « mods » : pour annuler, supprimez cette copie.\r\n\r\n" +
            "Aucun fichier du jeu n'a été modifié par ce programme.",
            "Préparation pour OpenIV", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------- Historique / désinstallation
    void RefreshHistory()
    {
        records = history.LoadAll();
        lstHistory.Items.Clear();
        foreach (var r in records)
            lstHistory.Items.Add((r.Status == InstallStatus.Installed ? "[Installé]     " : "[Désinstallé] ") + r.ModName + "  —  " + r.InstalledAt.ToString("dd/MM/yyyy HH:mm"));
    }

    async Task OnUninstallAsync()
    {
        try
        {
            var i = lstHistory.SelectedIndex;
            if (i < 0 || i >= records.Count) { MessageBox.Show(this, "Sélectionnez d'abord un mod dans la liste.", "Désinstaller"); return; }
            var rec = records[i];
            if (rec.Status != InstallStatus.Installed) { MessageBox.Show(this, "Ce mod est déjà désinstallé.", "Désinstaller"); return; }
            var ans = MessageBox.Show(this,
                "Désinstaller « " + rec.ModName + " » ?\r\n\r\nLes fichiers ajoutés seront supprimés et les fichiers remplacés seront restaurés depuis la sauvegarde.\r\n(L'éventuelle ligne ajoutée dans dlclist.xml via OpenIV doit être retirée à la main.)",
                "Confirmer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;
            var res = await Task.Run(() => installer.Uninstall(rec));
            if (res.Skipped > 0)
            {
                var force = MessageBox.Show(this, string.Join("\r\n", res.Messages) + "\r\n\r\nCertains fichiers n'ont pas été traités (modifiés depuis l'installation ou introuvables). Les traiter quand même ?",
                    "Fichiers modifiés", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (force == DialogResult.Yes) res = await Task.Run(() => installer.Uninstall(rec, true));
            }
            RefreshHistory();
            MessageBox.Show(this, string.Join("\r\n", res.Messages), "Résultat", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError("La désinstallation a échoué", ex); }
    }

    void ShowError(string title, Exception ex)
        => MessageBox.Show(this, title + " :\r\n\r\n" + ex.Message + "\r\n\r\nAucun fichier d'origine du jeu n'a été modifié.", "GTA Mods Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
