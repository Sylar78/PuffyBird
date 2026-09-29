# CLAUDE.md

Ce fichier guide Claude Code (claude.ai/code) quand il travaille dans ce dépôt.

## Projet

PuffyBird est un équivalent de Flappy Bird : un tap fait sauter l'oiseau, il faut passer entre des paires de tuyaux, un contact avec un tuyau ou le sol termine la partie. Le projet reprend la **mécanique** (non protégeable) avec un nom et des **assets originaux**.

Langue du projet : **français** (docs, messages de commit, échanges). Les identifiants de code et les constantes restent en anglais (`GRAVITY`, `FLAP_VELOCITY`…).

## État du dépôt

Au 29/09/2026, le dépôt ne contient que ce fichier. **Aucune stack n'est encore choisie** : ne pas en imposer une sans accord explicite. Il n'y a donc encore ni commande de build, de lint ni de test ; mettre à jour la section « Commandes » dès qu'une stack est retenue.

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
- Pixel art : filtrage au plus proche voisin, jamais de lissage.
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

## Commandes

Aucune pour l'instant (pas de stack). À compléter avec build, lancement local, lint et tests dès le premier code.
