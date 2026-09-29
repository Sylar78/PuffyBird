# CLAUDE.md

Ce fichier guide Claude Code (claude.ai/code) quand il travaille dans ce dépôt.

## Projet

PuffyBird est un équivalent de Flappy Bird : un tap fait sauter l'oiseau, il faut passer entre des paires de tuyaux, un contact avec un tuyau ou le sol termine la partie. Le projet reprend la **mécanique** (non protégeable) avec un nom et des **assets originaux**.

Langue du projet : **français** (docs, messages de commit, échanges). Les identifiants de code et les constantes restent en anglais (`GRAVITY`, `FLAP_VELOCITY`…).

## État du dépôt

Au 29/09/2026, le dépôt ne contient que ce fichier, sans code. La stack **recommandée** pour la version mobile iOS / Android est **Unity 6 + URP** (voir « Stack mobile et rendu » plus bas) ; elle reste à confirmer par le propriétaire du projet avant le premier code. Il n'y a donc encore ni commande de build, de lint ni de test ; mettre à jour la section « Commandes » dès que le projet Unity existe.

Documents de référence (hors dépôt, dans les fichiers partagés du projet Claude, `docs/`) :

| Document | Contenu |
|---|---|
| `flappy-bird-spec.md` | Spécification complète et indépendante de la technologie : règles, physique, états, collisions, UI, audio, direction artistique, architecture, critères d'acceptation, constantes (Annexe A). **Source de vérité du gameplay.** |
| `puffybird-reference.html` | Implémentation de référence jouable, un seul fichier HTML5/JS, sprites et sons générés par le code (aucun asset externe). Couvre les étapes 2 à 9 de la feuille de route. |
| `strategie-croissance.md` | Stratégie de croissance : modèle gratuit + pub, achats intégrés, acquisition digitale, distribution web puis stores. |

La spec prévoit de placer ces documents sous `docs/` dans le dépôt ; tant que ce n'est pas fait, ne pas supposer qu'ils y sont.

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

## Architecture visée

Modules de la spec (§18) : `config`, `main`, `game`, `bird`, `pipes`, `ground`, `collision`, `score`, `storage`, `audio`, `input`, `render`, `ui`, et un dossier `tests/`.

Principes :

- **Toutes les constantes dans `config`**, aucune valeur en dur ailleurs.
- **Simulation séparée du rendu** : la logique doit tourner et se tester sans affichage.
- **Aléatoire injectable** (`rng` avec graine) pour des parties reproductibles (tests, rediffusions, défi quotidien).
- **Aucune allocation en jeu** : recycler les tuyaux (pool de 4).
- Rendu : le prototype HTML est en pixel art (filtrage au plus proche voisin) ; la version mobile vise un rendu éclairé 2.5D (voir plus bas), ce qui remplace le style pixel art.
- Persistance sous les clés `puffybird.best`, `puffybird.muted` (web : `localStorage` toujours dans un `try/catch`).

En cas de port vers un moteur dont le repère diffère (Unity : y vers le haut, 1 unité = 100 px), suivre la table de correspondance §20.2 de la spec.

## Tests et validation

Les critères d'acceptation A1 à A15 (§23.1) et les tests unitaires suggérés (§23.2) définissent le « terminé ». Tester en priorité la physique (flap, vitesse terminale, hauteur de saut ≈ 40,5 px), les bornes de `gapTop` sur 10 000 tirages, l'espacement exact, le score unique par paire, la collision au-dessus d'un tuyau et les seuils de médailles. Le bot de validation (§23.3) sert de test de non-régression des réglages.

## Contraintes produit et juridiques

- Ne jamais utiliser le nom « Flappy » ni les sprites ou sons originaux de Flappy Bird. Assets générés par code, faits maison ou sous licence CC0 uniquement.
- Éviter des tuyaux verts « style Mario » identiques à l'original.
- Publicité : **jamais pendant une partie**, rien dans la zone de jeu, pas d'interstitiel lors de la première session, vidéo longue uniquement récompensée et choisie par le joueur.
- Achats intégrés cosmétiques ou retrait des pubs uniquement, jamais « pay-to-win ».
- Les extensions (skins, défi quotidien, tuyaux mobiles…) restent hors du mode principal.
- Distribution prévue : web d'abord (GitHub Pages, itch.io), puis portails web, puis stores mobiles.

## Stack mobile et rendu (recommandation du 29/09/2026, à confirmer)

Objectif : une version iOS / Android plus belle que le prototype, avec ombres, lumières, vertex shaders et effets proches du raytracing, en 2D ou 2.5D, sans toucher au gameplay de la spec.

### Choix recommandé : Unity 6 LTS + URP, en 2.5D

- **Moteur** : Unity 6 LTS, langage C#, export iOS (Metal) et Android (Vulkan, repli OpenGL ES 3).
- **Pipeline** : URP avec le renderer universel (3D) en mode **Forward+**. Décor et personnages en modèles 3D stylisés, **caméra en perspective légère** fixée de profil : c'est la 2.5D. Le gameplay reste strictement dans le plan 2D de la spec.
- **Ombres et lumières** : lumière directionnelle avec ombres temps réel (cascades réduites), quelques lumières ponctuelles (soleil couchant, lucioles, néons de nuit), lumière baked ou Adaptive Probe Volumes pour le décor statique, SSAO léger.
- **Shaders** : Shader Graph pour les vertex shaders (battement d'ailes, herbe et arbres au vent, tuyaux qui « respirent » à l'impact, eau), matériaux toon/rim light, dissolve à la mort.
- **Post-traitement** (Volume URP) : bloom, tonemapping ACES, color grading jour/nuit, vignette, profondeur de champ discrète sur l'arrière-plan.
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

Aucune pour l'instant (projet Unity pas encore créé). À compléter avec ouverture du projet, build iOS / Android, lancement des tests EditMode et PlayMode dès le premier code.
