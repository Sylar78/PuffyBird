# CLAUDE.md

Ce fichier guide Claude Code (claude.ai/code) quand il travaille dans ce dépôt.

## Projet

PuffyBird est un équivalent de Flappy Bird : un tap fait sauter l'oiseau, il faut passer entre des paires de tuyaux, un contact avec un tuyau ou le sol termine la partie. Le projet reprend la **mécanique** (non protégeable) avec un nom et des **assets originaux**.

Langue du projet : **français** (docs, messages de commit, échanges). Les identifiants de code et les constantes restent en anglais (`GRAVITY`, `FLAP_VELOCITY`…).

## État du dépôt

Projet **Unity 6 LTS + URP**, en 2.5D, pour iOS et Android (choix validé le 29/09/2026, voir « Stack mobile et rendu »). Toute la scène est construite par code : aucun modèle 3D, texture, police ni son importé. Les fichiers `.meta` et les réglages `ProjectSettings/` sont générés par Unity à la première ouverture et doivent ensuite être versionnés.

Documents de référence (hors dépôt, dans les fichiers partagés du projet Claude, `docs/`) :

| Document | Contenu |
|---|---|
| `flappy-bird-spec.md` | Spécification complète et indépendante de la technologie : règles, physique, états, collisions, UI, audio, direction artistique, architecture, critères d'acceptation, constantes (Annexe A). **Source de vérité du gameplay.** |
| `puffybird-reference.html` | Implémentation de référence jouable, un seul fichier HTML5/JS, sprites et sons générés par le code (aucun asset externe). Couvre les étapes 2 à 9 de la feuille de route. |
| `strategie-croissance.md` | Stratégie de croissance : modèle gratuit + pub, achats intégrés, acquisition digitale, distribution web puis stores. |

La spec prévoit de placer ces documents sous `docs/` dans le dépôt ; tant que ce n'est pas fait, ne pas supposer qu'ils y sont. Le `docs/` du dépôt contient les guides `publication-ios-testflight.md`, `publication-app-store.md`, `publication-android-google-play.md`, `classement-en-ligne.md` (Game Center, Play Games), `achats-integres.md`, `mesure-audience.md` et `fiches-stores-multilingues.md` (textes des fiches App Store et Google Play en cinq langues).

## Règles de gameplay à respecter

Toutes les valeurs sont dans un repère logique **288 × 512** (portrait 9:16), en pixels logiques et secondes, y vers le bas. Constantes principales (liste complète : Annexe A de la spec) :

| Constante | Valeur |
|---|---|
| `STEP` (pas fixe) | 1/60 s |
| `GRAVITY` | 900 px/s² |
| `FLAP_VELOCITY` | −270 px/s (remplace la vitesse, ne s'y ajoute pas) |
| `MAX_FALL_SPEED` | 300 px/s |
| `SCROLL_SPEED` | 120 px/s (tuyaux et sol) |
| `PIPE_W` / `PIPE_GAP` / `PIPE_SPACING` | 52 / 100 / 150 px |
| `GAP_TOP_MIN` / `GAP_TOP_MAX` | 80 / 220 px |
| `GROUND_Y` | 400 px |
| `BIRD_X` / `BIRD_RADIUS` | 57 px / 11 px (hitbox circulaire) |
| Médailles | 10 / 20 / 30 / 40 points |

Points qui se trompent facilement :

- **Pas de simulation fixe** avec accumulateur (delta plafonné à 0,25 s) : le comportement doit être identique à 30, 60 et 144 Hz.
- Le **haut de l'écran ne tue pas**, mais on ne peut pas passer au-dessus d'un tuyau (le tuyau du haut est infini).
- Le score augmente de 1 quand le centre de l'oiseau dépasse le centre d'un tuyau, **une seule fois par paire**.
- Machine à états : `TITLE` → `READY` → `PLAYING` → `DYING` → `OVER`. En `READY`, l'oiseau flotte sans tomber ; le premier tap lance la partie **et** fait sauter l'oiseau. En `OVER`, les entrées sont ignorées pendant 0,8 s.
- Le meilleur score ne doit **jamais** régresser : si la lecture échoue, partir de 0 sans écraser la valeur existante.

## Architecture

| Dossier | Assembly | Rôle |
|---|---|---|
| `Assets/PuffyBird/Scripts/Core/` | `PuffyBird.Core` (sans référence à Unity) | Simulation complète : `GameConfig` (toutes les constantes), `GameSimulation` (machine à états, §19), `Bird`, `PipeField` (pool circulaire de 4 paires, paires mobiles), `StarField` (étoiles de vitesse), `Collision`, `FixedStepClock`, `Rng` (graine), `OverScreenTimeline`, `SfxRecipes` (synthèse des 5 sons + celui de l'étoile), `AutoPilot` (bot §23.3), `MusicThemes` + `MusicSynth` (une boucle par décor, synthèse temps réel sans allocation), `Skins` + `Products` (catalogue des oiseaux, IDs des achats), `DailyStreak` (série de jours consécutifs), `DailyChallenge` (graine du jour), `Achievements` (`AchievementTracker`, 9 succès), `PerformanceMonitor` (mesure de fluidité), `Language` |
| `Assets/PuffyBird/Scripts/Runtime/` | `PuffyBird.Runtime` | `PuffyBirdGame` (point d'entrée : boucle à pas fixe, entrées, synchronisation du rendu, actions des boutons), `InputReader` (taps positionnés), `PlayerPrefsScoreStorage`, `GamePrefs` (réglages du joueur) |
| `Scripts/Runtime/Feedback/`, `Social/`, `Store/`, `Ads/` | idem | `Haptics` (vibrations iOS / Android par JNI) ; `Leaderboard` (Game Center, Play Games v2 par JNI : classement et succès), `ShareService` (message + capture de l'écran de fin), `SocialIds` ; `IStore` + `StoreServices` (boutique branchée par Monetization) ; `IBannerAds`, `IRewardedAds` + `AdServices` (bannière, vidéo récompensée, consentement) ; `Metrics/` : `IGameMetrics` + `MetricsServices` (mesure d'audience) |
| `Scripts/Runtime/Rendering/` | idem | `WorldSpace` (px logiques → monde), `CameraRig`, `LightingRig`, `PostFxController`, `SceneryView` (sol, collines, nuages et ensembles par décor), `WeatherView` (pluie, neige, pétales, feuilles, bulles, éclairs), `PipeView`, `StarView`, `BirdView` (phénix en armure du catalogue : heaume à couronne de pointes, ailes à épaulières et médaillons, plumes cerclées de flammes, queue de rubans ; aperçu agrandi du menu, oiseau doré pailleté sous étoile), `BoostTrailView` (traînée aux couleurs du phénix, `Palette.TrailColors`), `MeshBuilder` (maillages procéduraux), `MaterialLibrary`, `Palette` (dont `Skin`), `GraphicsQuality` (LOW / MEDIUM / HIGH) |
| `Scripts/Runtime/UI/`, `Audio/` | idem | `UiLayer` (textes, panneaux, boutons et test des taps), `HudView`, `MenuView` (boutons titre / fin, paramètres, aussi en partie), `SkinsView` (menu OISEAUX), `ConsentView` (RGPD : pubs et statistiques), `Lang` (français, anglais, espagnol, allemand ou portugais selon la langue du téléphone), `VoxelFont` (police 5 × 7 avec accents européens, et icônes) ; `SfxPlayer` (bruitages), `MusicPlayer` (`OnAudioFilterRead`) |
| `Assets/Plugins/iOS/` | — | Ponts natifs : `ATTRequester`, `HapticsBridge`, `GameCenterBridge`, `ShareBridge` |
| `Assets/PuffyBird/Resources/Shaders/` | — | `PuffyStylizedLit` (éclairage URP complet + liseré + déformations de sommets partagées par toutes les passes + paillettes ; l'alpha des couleurs de sommet masque `_VertexEmission`), `PuffySky` |
| `Assets/PuffyBird/Scripts/Editor/` | `PuffyBird.Editor` | `ProjectSetup` (URP mobile, réglages iOS / Android, icône, scène `Main`), `BuildScript` (options `-buildNumber`, `-appleTeamId`, `-customBuildPath`), `IconImportSettings` (icône sans alpha), `IosPostBuild` (Info.plist, GameKit et droit Game Center), `AndroidPostBuild` (permissions `AD_ID` et `VIBRATE`, Play Games si `SocialIds` est rempli), `PackageDefines` (symboles `PUFFYBIRD_IAP` et `PUFFYBIRD_ANALYTICS` si les packages Unity IAP et Analytics sont installés), `UnityCloudProject` (liaison Unity Cloud et rapport de plantages), `CompileCheck` (compilation des scripts sans build, pour la CI) ; signature Android par les options GameCI `-androidKeystoreName`… |
| `Assets/PuffyBird/Icons/` | — | `AppIcon.png`, générée par `tools/icon/make_icon.py` (Python sans dépendance) |
| `Assets/PuffyBird/Tests/EditMode/` | `PuffyBird.Tests.EditMode` | Tests NUnit de la simulation (critères A1 à A13) |
| `tools/CoreTests/` | — | Projet .NET qui compile `Core` et les tests EditMode hors de Unity |
| `.github/workflows/tests.yml` | — | `dotnet test tools/CoreTests` à chaque push et pull request |
| `.github/workflows/unity-compile.yml` | — | Compilation Unity avec les vrais packages (GameCI, `CompileCheck.Run`) quand `Assets/` ou `Packages/` change |
| `.github/workflows/ios-testflight.yml`, `ci/ios/` | — | Build iOS (GameCI sous Linux) puis archive, signature et envoi TestFlight sur un Mac GitHub |
| `.github/workflows/android-play.yml` | — | Bundle Android signé (GameCI sous Linux), envoi Google Play si le compte de service est configuré |

Repère monde : 1 unité = 100 px, y vers le haut, le sol (y logique 400) à y = 0, le plan de jeu à z = 0, la caméra à z = −8 regardant vers +z. Toujours convertir via `WorldSpace`.

Formats d'écran (§21.3) : jusqu'au 3:4 (`CameraRig.MaxAspect`), la vue s'élargit (option « extension ») et la simulation reçoit la largeur visible en plus (`GameSimulation.ViewMargin`, bornée par `MaxViewMargin` = 64 px) pour faire apparaître et disparaître les tuyaux hors champ, sans changer le gameplay. Au-delà du 3:4, bandes noires sur les côtés. Les boucles de décor (`SceneryView`) sont dimensionnées pour ce format maximal : tout élément ne se recycle qu'hors champ.

Principes :

- **Toutes les constantes dans `config`**, aucune valeur en dur ailleurs.
- **Simulation séparée du rendu** : la logique doit tourner et se tester sans affichage.
- **Aléatoire injectable** (`rng` avec graine) pour des parties reproductibles (tests, rediffusions, défi quotidien).
- **Aucune allocation en jeu** : tout est créé au chargement (tuyaux, particules, textes, chiffres), puis seulement déplacé, affiché ou masqué. `MeshBuilder` et `VoxelFont.Build` ne s'appellent qu'au chargement.
- Les shaders vivent dans un dossier `Resources` pour être inclus dans les builds sans matériau sur disque ; les matériaux sont créés par `MaterialLibrary`.
- Rendu : le prototype HTML est en pixel art ; la version mobile est un rendu éclairé 2.5D, qui remplace le style pixel art.
- Persistance sous les clés `puffybird.best`, `puffybird.muted` (bruitages), `puffybird.music`, `puffybird.haptics`, `puffybird.quality`, `puffybird.consent`, `puffybird.skin`, `puffybird.owned` (achats), `puffybird.streak` / `puffybird.streakday` / `puffybird.streakbest` (série de jours), `puffybird.dailyday` / `puffybird.dailybest` (défi du jour), `puffybird.ach` / `puffybird.achsent` (succès gagnés et envoyés), `puffybird.qualityauto`, `puffybird.reduceflash`, `puffybird.contrast` (PlayerPrefs).
- Interface : chaque image, les vues redéclarent leurs boutons (`UiLayer.BeginFrame` / `PlaceButton`) ; un tap sur un bouton déclenche une `UiAction`, sinon il va à la simulation. Un menu ouvert bloque le lancement de la partie. Exceptions à « VoxelFont au chargement » : les prix localisés, le texte « SÉRIE : n / 30 JOURS » et « MEILLEUR AUJOURD'HUI : n », construits hors partie et refaits seulement quand leur valeur change.

Écarts assumés par rapport à la spec :

- `MaxRiseSpeed` = −270 au lieu de −240 : avec −240 le saut ne fait que ≈ 34 px, en contradiction avec §6.3 et le test §23.2 (40,5 px). La spec (§6.4) autorise cette simplification.
- Écran de fin : un tap n'importe où (après 0,8 s) relance la partie, au lieu d'un bouton Play dédié.
- `AutoPilot` cherche une suite de battements sûre sur ≈ 1,7 s au lieu du bot trivial de §23.3, qui meurt sur certaines combinaisons d'ouvertures. Il survit sur toutes les graines testées : la difficulté reste juste.
- Textes du jeu en cinq langues (français, anglais, espagnol, allemand, portugais) selon la langue du téléphone, anglais pour toute autre (`UI/Lang.T(fr, en, es, de, pt)`, choisi au chargement ; noms des oiseaux : `SkinInfo.NameIn(Language)`, `Core/Language`). La police gère les accents (À Á Â Ã Ä É È Ê Ë Í Î Ï Ñ Ó Ô Õ Ö Ú Ù Û Ü Ç) au-dessus de la ligne, et ¡ ¿ ; pas de ß (écrire SS). Un texte trop long pour son bouton ou son panneau est réduit automatiquement (`UiLayer.Place` / `PlaceButton`, `Element.TextWidth`). Tout nouveau libellé doit être fourni dans les cinq langues.
- Score en jeu placé tout en haut de l'écran visible, sous l'encoche (`CameraRig.SafeTopPx` + `ScoreTopMargin`), au lieu de y = 50 : il gênait la visibilité.
- Apparence de l'oiseau choisie par le joueur (menu OISEAUX, `Core/Skins`) au lieu du tirage de couleur de §6.8 ; cinq phénix en armure, un par élément (`Palette.Skin` / `PhoenixColors`, demande du 05/10/2026 d'après des images de référence : FEU offert, id `phoenix` ; EAU, médaille de bronze, id `blue` ; VENT, médaille d'or, id `mint` ; LUMIÈRE, série de 30 jours, id `streak` ; TÉNÈBRES, achat, id `ninja` ; identifiants d'origine gardés pour les choix enregistrés et le produit). Le modèle s'inspire du style des images (armure, gemmes, plumes de flammes) sans copier un personnage existant, lueur par l'alpha des couleurs de sommet, purement visuel : hitbox inchangée ; le tirage reste consommé par la simulation pour garder la suite des ouvertures de la spec.
- Pas d'indicateur « SOUND OFF » à l'écran : réglages MUSIQUE et SONS séparés dans PARAMÈTRES (la touche M coupe les bruitages).
- Paramètres accessibles pendant la partie (engrenage en haut à droite en `READY`, `PLAYING` et `PAUSED`) : les ouvrir met la partie en pause ; le panneau propose alors ACCUEIL, qui abandonne la partie sans enregistrer de score (`GameSimulation.QuitToTitle` : fondu, puis écran titre avec un nouveau décor). En fermant le panneau, la partie reste en pause jusqu'au prochain tap.
- Neuf décors au lieu de deux (`Theme` : jour, nuit, ville au crépuscule, Japon médiéval, orage, neige, jungle tropicale, ciel, fond marin ; les trois derniers ajoutés le 02/10/2026), tirés au hasard à chaque partie (`ThemeCount`, égal au nombre de valeurs de `Theme`, une musique chacun). Ensembles de décor (`Palette.SetPiece`), météo (`Palette.Weather`) et présence des nuages (`Clouds`, absents sous la mer) dans `Palette.Theme`. La pluie, la neige, les pétales, les feuilles et les bulles (qui remontent) passent derrière le plan de jeu ; les éclairs sont atténués avec `reduceFlash`. Le décor reste purement visuel : le sol tue toujours, même en ciel (mer de nuages) ou sous l'eau (sable).
- Sol : gazon rayé doux en niveaux de gris teinté par le décor, bordure festonnée, touffes d'herbe au vent (courtes devant le plan de jeu) et fleurs.

Extensions intégrées au mode principal (demandées le 30/09 et le 02/10/2026, hors spec) :

- **Tuyaux mobiles** : à partir de 15 points (`MovingPipesFromScore`), une nouvelle paire sur deux environ (`MovingPipeChance`, tirage, demande du 06/10/2026) monte et descend de ± 30 px (30 % de l'ouverture, `PipeMoveAmplitude`, période 2,6 s) ; les autres restent fixes. Les deux tuyaux bougent ensemble, l'ouverture garde 100 px ; `PipePair.GapTop` reste la valeur tirée, le haut réel est `OpeningTop` (`BaseTop` + décalage).
- **Tuyaux seuls** (06/10/2026) : à partir de la 6e paire (`SinglePipeFromPair`), environ 12 % des paires (`SinglePipeChance`) n'ont qu'un tuyau, plus long (`PipeKind.TopOnly` / `BottomOnly`) : l'ouverture de 100 px est entre lui et le sol, ou entre lui et le haut de l'écran (rien au-dessus, on peut y passer hors champ). Le côté suit l'ouverture précédente (haute → ouverture en haut, basse → en bas), jamais deux de suite, et la paire suivante est à `AfterSinglePipeSpacing` = 225 px pour laisser le temps de rejoindre son ouverture. `GapTop` garde la valeur tirée : la suite des ouvertures de la spec est inchangée.
- **Pauses** (06/10/2026) : à partir de la 7e paire (`BreatherFromPair`), environ 10 % des intervalles (`BreatherChance`) mesurent `BreatherSpacing` = 330 px au lieu de 150 : un moment sans tuyau pour souffler. L'espacement de chaque paire est donc 150, 225 ou 330 px (`PipePair.SpacingBefore`, choisi par `PipeField.NextSpacing`), toujours exact, étoile comprise.
- **Étoiles de vitesse** : environ 15 % des paires (à partir de la 4e, `StarChance`) ont une étoile à mi-chemin de la précédente. La toucher accélère le défilement de 15 % (`StarBoostFactor`, 30 % jusqu'au 05/10/2026) ; 5 s après la prise, la vitesse est redevenue normale (montée et retour de 0,3 s compris). L'espacement des tuyaux reste exact. Tant que l'effet dure, l'étoile protège d'un tuyau (demande du 08/10/2026) : le toucher la consomme au lieu de tuer (`GameSimulation.Shielded`, `GameEvents.StarShield`), l'oiseau traverse cette paire (`PipePair.Pierced`), la vitesse et l'éclat redescendent ; le sol tue toujours. Effets : le phénix brille de sa propre couleur (plumage éclairci et lumineux `Palette.Radiant`, lueur `BoostGlow`, paillettes et éclats `BoostGlitter`, demande du 06/10/2026, au lieu du doré commun), traînée et gerbe d'étincelles aux couleurs du phénix choisi, arpège.
- **Seconde chance** : sur l'écran de fin, bouton CONTINUER (PUB) dès qu'une régie de vidéos est branchée (`IRewardedAds.Enabled`), une fois par partie (`GameSimulation.CanContinue` / `ContinueRun`) : l'oiseau repart en `READY` avec le même score et le même décor, tuyaux et étoiles retirés. Au tap, la vidéo est montrée dès qu'elle est chargée ; si aucune ne l'est après `ContinueAdWait` = 3 s (pas de réseau, pas de pub disponible), la partie reprend sans vidéo (`PuffyBirdGame.UpdateContinue`). La vidéo n'est jamais imposée ; sa durée dépend des régies (réglable dans LevelPlay).
- **Série de jours** (demandée le 02/10/2026) : jouer au moins une partie par jour (premier tap, `GameEvents.RunStarted`) fait monter la série (`Core/DailyStreak`, jours locaux ; un jour manqué la remet à 1, une horloge reculée ne la casse pas). À `StreakDaysForSkin` = 30 jours consécutifs, le phénix LUMIÈRE (`SkinUnlock.Streak`, id `streak`, armure d'or et plumes blanches) est débloqué pour toujours (`GamePrefs.LongestStreak`) ; le menu OISEAUX affiche « SÉRIE : n / 30 JOURS ».
- **Sensation de jeu** (même date) : `GameEvents.NearMiss` quand l'oiseau a dépassé un tuyau en passant à moins de `NearMissDistance` = 5 px (une fois par paire, jamais s'il touche) : étincelles, « tink », vibration moyenne. `GameEvents.Milestone` quand le score atteint une médaille (10 / 20 / 30 / 40) : couronne d'étincelles aux couleurs de la médaille, fanfare, gonflement du score (`HudView.PulseScore`), léger tremblement de caméra (coupé avec `reduceFlash`). Purement visuel et sonore, hitbox inchangée.
- **Défi du jour** (demandé le 02/10/2026) : bouton DÉFI DU JOUR sur l'écran titre (`GameSimulation.StartDaily`, `UiAction.PlayDaily`). Même graine pour tous les joueurs le même jour (`DailyChallenge.SeedForDay`, jour local) : mêmes ouvertures, étoiles, mouvements et décor, car `Rng.Reseed` rejoue toute la suite à chaque essai. Les essais s'enchaînent sur la même graine jusqu'à ACCUEIL (`IsDaily`). Meilleur score du jour gardé à part (`GamePrefs.RecordDaily`), affiché sous le bouton et à l'écran de fin ; le meilleur score général reste commun.
- **Succès** : 9 (`Core/Achievements`, médailles 10 / 20 / 30 / 40, première étoile, 5 frôlements dans une partie, séries de 7 et 30 jours, défi du jour). Gardés localement (`GamePrefs.Achievements`) et envoyés à Game Center (`puffybird.ach.<clé>`) ou Play Games (`SocialIds.PlayGamesAchievements`, vide = non envoyé) dès que le joueur est connecté, une seule fois chacun. Mise en place : `docs/classement-en-ligne.md` ; icônes : `tools/achievements/make_icons.py`.
- **Partage en image** : le bouton PARTAGER envoie le message avec une capture de l'écran de fin, prise sans les boutons (`MenuView.HideOverlay`). iOS : `_Share_Image` ; Android : image ajoutée à la galerie par MediaStore (Android 10 ou plus), sinon texte seul.
- **Accessibilité** : réglages MOINS DE FLASHS (`GamePrefs.ReduceFlash`, en plus de l'option de l'inspecteur) et TUYAUX CONTRASTÉS (`Palette.PipeBodyContrast`, orangé vif à bague claire : seuls les maillages changent). Pas de tap « plus tolérant » : toucher à la hitbox changerait la difficulté.
- **Qualité automatique** : tant que le joueur n'a pas choisi sa qualité, `PerformanceMonitor` baisse d'un cran (retenu dans `puffybird.qualityauto`) si la cadence reste sous 80 % de la cible pendant deux fenêtres de 3 s en partie. Inactif dans l'éditeur.
- Ces tirages utilisent un second `Rng` dérivé de la graine : la suite des ouvertures reste celle de la spec. `AutoPilot` rejoue les mouvements et les accélérations (étoiles comprises) et survit sur toutes les graines testées.

## Tests et validation

Les critères d'acceptation A1 à A15 (§23.1) et les tests unitaires suggérés (§23.2) définissent le « terminé ». Tester en priorité la physique (flap, vitesse terminale, hauteur de saut ≈ 40,5 px), les bornes de `gapTop` sur 10 000 tirages, l'espacement exact, le score unique par paire, la collision au-dessus d'un tuyau et les seuils de médailles. Le bot de validation (§23.3) sert de test de non-régression des réglages.

## Contraintes produit et juridiques

- Ne jamais utiliser le nom « Flappy » ni les sprites ou sons originaux de Flappy Bird. Assets générés par code, faits maison ou sous licence CC0 uniquement.
- Éviter des tuyaux verts « style Mario » identiques à l'original.
- Publicité : **jamais pendant une partie**, rien dans la zone de jeu, pas d'interstitiel lors de la première session, vidéo longue uniquement récompensée et choisie par le joueur.
  - Bannière : la règle d'affichage vit dans `Core/AdPolicy.BannerVisible` (testée) : seulement sur l'écran titre et sur l'écran de fin une fois le score affiché, en bas de l'écran sous le sol, masquée dès le tap et jamais en `READY`, `PLAYING`, `DYING` ni en pause. Le SDK passe par l'interface `Runtime/Ads/IBannerAds` et `AdServices.BannerFactory`.
  - Intégration LevelPlay (package « Ads Mediation » 9.5.1) dans `Assets/PuffyBird/Monetization/`, **hors asmdef** (Assembly-CSharp référence le package automatiquement) : `AdIds` (App Key, ID de bannière et de vidéo récompensée par plateforme ; vides = pas de pub ; vidéo récompensée vide = pas de seconde chance ; les deux plateformes sont renseignées depuis le 02/10/2026), `LevelPlayRewarded` (seconde chance), `LevelPlayAds` (attend la réponse à l'écran de consentement RGPD du premier lancement, puis ATT iOS si accord, `SetGDPRConsent` / `SetCCPA`, puis `LevelPlay.Init`, bannière 320 × 50 en bas, rafraîchissement suspendu quand elle est cachée). Pont ATT natif : `Assets/Plugins/iOS/ATTRequester.mm` ; `IosPostBuild` ajoute le texte ATT, HTTP autorisé et le framework. Unité de bannière réglée à 30 s de rafraîchissement, app déclarée COPPA « Not directed » (13 ans et plus).
  - CI iOS : le résolveur de dépendances écrit un `Podfile` ; le job Mac lance `pod install` et archive le `.xcworkspace`.
- Achats intégrés cosmétiques ou retrait des pubs uniquement, jamais « pay-to-win ». Unity IAP 5 dans `Monetization/UnityIapStore` (non consommables `fr.puffybird.app.noads` et `fr.puffybird.app.skin.*`), compilé avec le symbole `PUFFYBIRD_IAP` (package `com.unity.purchasing` 5.4.3 déclaré dans le manifest depuis le 04/10/2026 ; produits à créer sur les stores, voir `docs/achats-integres.md`).
- Mesure d'audience : Unity Analytics 6.3 (`Monetization/UnityAnalyticsMetrics`, symbole `PUFFYBIRD_ANALYTICS`), seulement après accord sur l'écran de consentement (transmis à `EndUserConsent` sous Unity 6.2 et plus) ; projet Unity Cloud relié par `Editor/UnityCloudProject` (IDs vides = pas de mesure ni de rapport de plantages Diagnostics, car `ProjectSettings/` n'est pas versionné) ; événements `playerDied`, `continueUsed`, `quitToTitle`, `skinSelected` à déclarer dans le tableau de bord (`docs/mesure-audience.md`).
- Classement : Game Center (`puffybird.best`) ; Play Games tant que `SocialIds` est vide n'est pas inclus dans le build.
- Les autres extensions restent hors du mode principal ; tuyaux mobiles, étoiles de vitesse, série de jours et défi du jour y ont été intégrés à la demande (voir « Architecture »). Les oiseaux (débloqués par médaille ou achetés) sont purement cosmétiques.
- Distribution prévue : web d'abord (GitHub Pages, itch.io), puis portails web, puis stores mobiles.

## Stack mobile et rendu (choix du 29/09/2026)

Objectif : une version iOS / Android plus belle que le prototype, avec ombres, lumières, vertex shaders et effets proches du raytracing, en 2D ou 2.5D, sans toucher au gameplay de la spec.

### Choix : Unity 6 LTS + URP, en 2.5D

- **Moteur** : Unity 6 LTS, langage C#, export iOS (Metal) et Android (Vulkan, repli OpenGL ES 3).
- **Pipeline** : URP avec le renderer universel (3D) en mode **Forward** (une lumière principale et au plus 3 lucioles : Forward est le plus économique sur mobile ; le shader gère aussi Forward+). Décor et personnages en modèles 3D stylisés, **caméra en perspective légère** fixée de profil : c'est la 2.5D. Le gameplay reste strictement dans le plan 2D de la spec.
- **Ombres et lumières** : soleil (jour) ou lune (nuit) avec ombres douces temps réel, lucioles en lumières ponctuelles la nuit, lumière ambiante en harmoniques sphériques, brouillard atmosphérique. SSAO possible en ajoutant la Renderer Feature au renderer.
- **Shaders** : écrits à la main en HLSL (pas de Shader Graph, pour tout garder en texte versionnable) : vent sur arbres et buissons, gonflement des nuages et de l'oiseau, vibration du tuyau touché, flexion des ailes, liseré lumineux.
- **Post-traitement** (Volume URP créé par code) : bloom, tonemapping Neutral, étalonnage jour/nuit, vignette, profondeur de champ gaussienne sur le lointain ; le flash d'impact et le fondu au noir passent par l'exposition.
- **Raytracing** : le raytracing matériel n'est **pas** disponible dans URP, et HDRP ne cible pas le mobile. Seuls les téléphones haut de gamme récents ont du RT matériel, donc pas la cible grand public. On obtient l'effet visuel autrement : réflexions par reflection probes et SSR factice, ombres de contact, GI précalculée, et au besoin une passe maison (Render Graph) d'ombres douces 2D par SDF / raymarching limitée au plan de jeu.
- **Performance** : 60 FPS stables sur un milieu de gamme de 2021, attention à la chauffe. Prévoir un réglage de qualité (Bas / Moyen / Haut) qui coupe ombres, SSAO et post-traitement coûteux.

### Pourquoi Unity plutôt qu'une autre stack

| Stack | Verdict |
|---|---|
| **Unity 6 + URP** | Retenu : meilleur rapport rendu mobile / outillage (Shader Graph, lumières 2D et 3D, post-traitement), écosystème pub (LevelPlay), achats intégrés, services (classements, remote config, analytics), exports iOS et Android éprouvés, et export web possible. |
| Godot 4 | Bon second choix, gratuit et léger, lumières 2D et ombres correctes, mais renderer mobile plus limité et intégrations pub / IAP moins matures. |
| Unreal Engine 5 | Rendu superbe mais taille d'appli et coût GPU disproportionnés pour un jeu de sessions de 30 s. |
| Flutter + Flame, Capacitor (web) | Trop limités pour ombres dynamiques et shaders avancés ; à garder pour la version web simple. |

Alternative plus légère si la 3D coûte trop : **URP 2D Renderer** (sprites avec normal maps, Light 2D, Shadow Caster 2D, parallaxe multi-couches). Même moteur, bascule possible au début du projet.

### Règles de port de la spec vers Unity

- Le gameplay n'utilise **pas** la physique Unity (`Rigidbody`) : simulation maison déterministe en C# pur, pas fixe 1/60 via un accumulateur (ou `FixedUpdate` avec `Time.fixedDeltaTime = 1/60`), testable en EditMode sans scène.
- Unity a y vers le haut : inverser les signes de `GRAVITY` et `FLAP_VELOCITY`. Garder les constantes de la spec en px dans `config` et convertir au rendu (échelle unique, par exemple 1 unité = 100 px).
- Le rendu (modèles, lumières, shaders) ne fait que **lire** l'état de la simulation ; aucun effet visuel ne modifie les hitbox.
- Les ombres et le décor 3D ne doivent jamais masquer un tuyau ni l'oiseau (lisibilité avant tout, §16.4 de la spec).

### Skills Claude à utiliser pour ce projet

Skills du plugin Unity, dans l'ordre d'usage :

| Étape | Skill | Usage |
|---|---|---|
| Démarrage | `unity:new-unity-project` | Créer le projet Unity 6 (cibles iOS / Android, monétisation) |
| Démarrage | `unity:unity-cli`, `unity:unity-package-management` | Piloter l'éditeur, builder, tester, installer les packages (URP, Shader Graph, Input System…) |
| Rendu | `unity:urp-postprocessing` | Bloom, tonemapping, color grading, vignette, profondeur de champ |
| Rendu | `unity:shader-graph-create-custom-node` | Nœuds HLSL pour vertex shaders et effets maison |
| Rendu | `unity:validate-urp-render-graph-renderer-feature` | Valider une passe maison (ombres SDF, outline) |
| Rendu 2D | `unity:manage-sprite-atlas`, `unity:sprite-editor` | Si l'option URP 2D Renderer est retenue |
| Interface | `unity:ui`, `unity:ui-uitk`, `unity:optimize-text-mesh-pro` | Écrans titre, Get Ready, fin de partie, score |
| Audio | `unity:audio-setup-mixers`, `unity:optimize-audio` | Mixer musique / effets, formats mobiles |
| Monétisation | `unity:levelplay-unity-integration` | Pubs (bannière, interstitiel, vidéo récompensée) selon les règles ci-dessus |
| Monétisation | `unity:implement-in-app-purchases` | « Sans pub » et skins cosmétiques |
| Live ops | `unity:build-live-game` | Classements, défi quotidien, remote config, analytics, cloud save |
| International | `unity:localization` | Traductions (FR, EN puis autres) |
| Web | `unity:optimize-web` | Seulement si on publie aussi la version Unity sur le web |

Skills génériques utiles : `code-review` et `security-review` avant chaque PR, `session-start-hook` pour préparer les sessions Claude dans le cloud (tests C# EditMode en ligne de commande), `simplify` pour le nettoyage.

Skills inutiles ici : `unity:migrate-birp-to-urp` (le projet démarre directement en URP), `unity:2d-pixel-perfect` (le style pixel art est abandonné pour le mobile), et les skills multijoueur, voix et navigation IA.

Skills de projet à créer plus tard dans `.claude/skills/` : un contrôle de conformité à la spec (constantes, critères A1 à A15), et une procédure de build et de publication iOS / Android.

### Contraintes d'environnement

- L'éditeur Unity et ses builds tournent sur la machine du développeur, pas dans un conteneur cloud : utiliser une session Claude en local (Remote Control ou Claude Code sur le poste).
- Un build iOS exige un Mac avec Xcode et un compte Apple Developer ; Android exige le module Android de Unity (SDK, NDK, JDK).

## Commandes

- **Tests de la simulation sans Unity** (possible dans une session cloud) : `dotnet test tools/CoreTests` (.NET 8).
- **Ouvrir le projet** : Unity Hub > Add > Add project from disk, avec Unity 6 LTS (6000.0 ou plus récent) et les modules iOS / Android. À la première ouverture, `ProjectSetup` configure URP et crée `Assets/PuffyBird/Scenes/Main.unity` ; relançable via le menu **PuffyBird > Configurer le projet**. Si Unity propose d'activer le nouvel Input System, accepter.
- **Jouer** : ouvrir la scène `Main`, Play. Touches : Espace / clic = tap, Échap ou P = pause (ou ferme le menu), M = bruitages, B = pilote automatique, C = capture d'écran dans `Captures/` (éditeur seulement, à la résolution de la vue Game : régler celle-ci sur la taille demandée par l'App Store).
- **Tests EditMode dans Unity** : Window > General > Test Runner, ou `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`.
- **Builds** : menu **PuffyBird > Build Android (APK)** / **Build iOS (projet Xcode)**, ou `Unity -batchmode -quit -projectPath . -executeMethod PuffyBird.Editor.BuildScript.BuildAndroid` (ajouter `-release` pour un `.aab`) et `...BuildScript.BuildIOS`.
- **TestFlight** : GitHub, onglet Actions > **iOS TestFlight** > Run workflow. Prérequis (compte Apple, clé API, licence Unity, secrets) : `docs/publication-ios-testflight.md`. La version Unity de la CI est fixée dans le workflow (`UNITY_VERSION`) : la garder alignée sur `ProjectSettings/ProjectVersion.txt`.
- **Google Play** : onglet Actions > **Android Google Play** > Run workflow (piste et statut au choix). Prérequis (clé d'envoi, secrets, premier envoi à la main) : `docs/publication-android-google-play.md`.
- **Icône** (le phénix sur un ciel de crépuscule) : `python3 tools/icon/make_icon.py`, puis menu **PuffyBird > Configurer le projet** pour l'appliquer.
