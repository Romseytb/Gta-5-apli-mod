# Guide utilisateur — GTA Mods Manager

## Premier lancement
Le programme cherche GTA V tout seul (par exemple `C:\Games\Rockstar Games\GTA 5`) et vous demande **« Est-ce le bon dossier ? »**. Répondez Oui : le chemin est mémorisé. S'il ne trouve rien, il vous demande de choisir le dossier qui contient `GTA5.exe`.
Il affiche aussi la version détectée (Legacy ou Enhanced). **Rien n'est modifié dans le jeu tant que vous n'avez pas confirmé le dossier et cliqué sur Installer.**

## Installer un mod
1. Glissez le ZIP (ou le dossier) du mod dans la grande zone bleue. Il est analysé **sans rien modifier**.
2. Lisez le résumé : type de mod, dépendances (par exemple VehFuncs V), avertissements, liste des fichiers.
3. Cliquez sur **Installer ce mod**. Une fenêtre liste exactement ce qui sera copié et demande confirmation.
4. Pour un véhicule **ajouté** (type DLC), une étape reste manuelle dans **OpenIV** : ajouter une ligne à `dlclist.xml`. Le programme copie la ligne dans le presse-papiers et vous explique où la coller.

## Mods de remplacement (comme le BearCat GIGN)
Ils remplacent un véhicule existant (ici `pbus`) et demandent de modifier une archive `.rpf` chiffrée du jeu. Le programme **prépare** les fichiers, ouvre leur dossier et vous guide dans OpenIV. Il ne le fait pas à votre place.

## Annuler / restaurer
Onglet **« Mods installés »** → sélectionnez le mod → **Désinstaller**. Les fichiers ajoutés sont supprimés, les fichiers remplacés sont restaurés depuis la sauvegarde. Les sauvegardes sont dans `%AppData%\GtaModsManager\sauvegardes`.

## Ce que le programme ne fait jamais
- Il n'écrit que dans les dossiers `mods`, `VehFuncsV` et `scripts` du jeu : jamais `GTA5.exe`, `update.rpf` ni les `.rpf` d'origine.
- Il ne remplace aucun fichier existant sans confirmation, après sauvegarde.
- Il refuse d'installer si GTA V est ouvert ou si le jeu est en version Enhanced.
- Il n'installe aucun logiciel tiers (OpenIV, Script Hook V, VehFuncs V) et ne touche pas à Mephisto.

## À savoir
- OpenIV est indispensable pour l'étape `dlclist.xml` et pour les mods de remplacement.
- Je ne connais pas le comportement de Mephisto : s'il répare ou vérifie les fichiers du jeu, il peut supprimer les mods. Réinstallez-les alors depuis l'onglet « Installer ».
- Formats pris en charge pour l'instant : ZIP et dossiers. Les `.rar` et `.7z` doivent être décompressés d'abord.
