namespace GtaModsManager;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (s, e) =>
            MessageBox.Show("Une erreur inattendue est survenue :\n\n" + e.Exception.Message + "\n\nAucun fichier du jeu n'a été modifié par cette erreur.",
                "GTA Mods Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm());
    }
}
