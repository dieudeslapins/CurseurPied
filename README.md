# 🐾 CurseurPied

Un lanceur Windows pour remplacer les curseurs système par un thème **pied / stylo tenu par un pied**, en couleurs chair, avec réglage de la taille d'affichage.

![Aperçu du curseur principal](curseurs/previews/arrow.png)

## Fonctionnalités

- **15 curseurs Windows personnalisés** (144×144) : flèche, lien, texte, attente animée, déplacement, redimensionnement, etc.
- **Lanceur graphique** `EquipeCurseurPied.exe` (icône pied intégrée) :
  - Appliquer tous les curseurs PIED
  - Restaurer les curseurs Windows d'origine
  - Cycle automatique toutes les 5 s (PIED / Windows)
  - Appliquer uniquement le curseur principal
  - Choisir la taille d'affichage Windows (`32 / 48 / 64 / 96 / 128 px`)
  - Lancer au démarrage de Windows (option)
- Les **points chauds** (hotspots) sont placés sur la partie fonctionnelle de chaque dessin (gros orteil ou pointe du stylo).

## Aperçu portable (nouveau)

`ApercuCurseur.exe` affiche l'état du curseur choisi : liste des 15 curseurs **plus l'entrée « GIF attente sans jambes (30 images) »**, aperçu agrandi avec le point chaud, animations pour `appstarting` / `wait` et pour le GIF, zoom, et option « voir aussi comme vrai curseur sur la fenêtre ». La vitesse des animations `.ani` est convertie correctement (jiffies → ms).

- **Choix du hitbox** : cliquez sur l'aperçu pour placer le point chaud (croix rouge), bouton « Reset point chaud » pour revenir au réglage d'origine. Le curseur réel utilise votre choix.
- **Stylo = Texte** : le curseur `nwpen.cur` est désormais une copie de `ibeam.cur`.
- **Un seul fichier** : les 15 curseurs **et le GIF nettoyé** sont intégrés dans l'EXE (16 ressources).
- **Aucune écriture** : ni fichier, ni dossier, ni registre, ni démarrage automatique.
- **Suppression propre** : supprimez l'EXE, il ne reste rien.

## Utilisation

1. Double-cliquer sur `EquipeCurseurPied.exe`.
2. Cliquer sur **« Appliquer TOUS les curseurs PIED »**.
3. Ajuster la taille avec le bouton **« Taille Windows »** si besoin.

> **Note importante :** Windows redimensionne les curseurs selon `HKCU\Control Panel\Cursors\CursorBaseSize`. Agrandir les fichiers `.cur` ne suffit pas ; le bouton « Taille Windows » modifie directement ce réglage.

## Structure du dépôt

| Chemin | Contenu |
|---|---|
| `EquipeCurseurPied.cs` | Code source du lanceur (C# / WinForms, .NET Framework 4) |
| `EquipeCurseurPied.exe` | Lanceur compilé, prêt à l'emploi (icône intégrée) |
| `EquipeCurseurPied.ico` | Icône du lanceur (16 → 256 px, pied chair) |
| `ApercuCurseur.exe` / `ApercuCurseur.cs` | Aperçu portable des 15 curseurs + GIF animé (16 ressources, aucune écriture disque/registre) |
| `attente_sans_jambes.gif` | GIF source nettoyé : 30 images, pieds seuls, fond transparent |
| `curseurs/` | Les 13 `.cur` et 2 `.ani` du thème |
| `curseurs/previews/` | Aperçus PNG de chaque curseur |
| `LISEZ-MOI-curseur.txt` | Documentation détaillée en français |

## Compilation

Le lanceur est compilé avec le compilateur C# fourni avec le .NET Framework (Windows) :

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' `
  /nologo /target:winexe /win32icon:EquipeCurseurPied.ico /out:EquipeCurseurPied.exe `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll `
  EquipeCurseurPied.cs
```

L'aperçu portable s'compile avec les curseurs intégrés en ressources :

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' `
  /nologo /target:winexe /win32icon:EquipeCurseurPied.ico /out:ApercuCurseur.exe `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll `
  /resource:curseurs\arrow.cur,arrow.cur /resource:curseurs\appstarting.ani,appstarting.ani `
  ... (un /resource par curseur) ...
  /resource:attente_sans_jambes.gif,attente_sans_jambes.gif
  ApercuCurseur.cs
```

## Images sources

- `5x3-grid-of-15.jpg` — grille d'origine (Grok) pour les 15 curseurs PIED
- `keep-the-exact-same.jpg` — utilisé pour la sélection normale, le lien, le déplacement et l'attente animée
- `Gemini_Generated_Image_sejxepsejxepsejx.jpg` — utilisé pour le curseur de texte (I-beam)
- `Sans titre.gif` — utilisé pour les curseurs animés **traitement en arrière-plan** (`appstarting.ani`) et **occupé** (`wait.ani`). Les jambes ont été supprimées : découpe finale `x = 25..335`, `y = 170..340`, juste avant la séparation des jambes (4 frames extraites sur 30).
- `attente_sans_jambes.gif` — version nettoyée du GIF source (30 frames, même découpe, fond transparent), fournie dans le dépôt et **affichable directement dans l'aperçu portable**.

Le fond damier des images a été converti en transparence ; les couleurs chair d'origine sont conservées.

## Licence

Projet personnel — libre d'utilisation.
