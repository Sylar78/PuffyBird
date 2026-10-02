# Mesure d'audience (Unity Analytics)

Le jeu envoie des statistiques d'usage à Unity Analytics **seulement si le joueur a accepté** sur l'écran de consentement (premier lancement, puis Paramètres > Confidentialité). S'il refuse ou retire son accord, la collecte s'arrête.

Sans le package, le jeu compile et tourne sans mesure : le code (`Assets/PuffyBird/Monetization/UnityAnalyticsMetrics.cs`) n'est compilé qu'avec le symbole `PUFFYBIRD_ANALYTICS`, posé automatiquement par `Editor/PackageDefines` quand le package est installé.

## Mise en place (une fois, dans Unity)

1. **Relier le projet à Unity Cloud** : Edit > Project Settings > Services, choisir l'organisation puis créer (ou lier) le projet « PuffyBird ». Le `ProjectSettings/` modifié est à pousser sur GitHub.
2. **Installer le package** : Window > Package Manager > Unity Registry > **Analytics** (`com.unity.services.analytics`, 6.x) > Install. Pousser `Packages/manifest.json` et `Packages/packages-lock.json`.
3. **Créer les événements** dans le tableau de bord (cloud.unity.com > Analytics > Event Manager). Unity refuse les événements qui n'y sont pas déclarés :

| Événement | Paramètres | Quand |
|---|---|---|
| `playerDied` | `score` (int), `theme` (string), `seconds` (float), `continued` (bool) | à chaque mort (`seconds` : durée de jeu depuis le premier tap ou la seconde chance) |
| `continueUsed` | `score` (int) | seconde chance obtenue après la vidéo |
| `quitToTitle` | `score` (int) | bouton ACCUEIL pendant une partie |
| `skinSelected` | `skinId` (string) | oiseau choisi dans le menu OISEAUX |

Les sessions, joueurs actifs (DAU / MAU), nouveaux joueurs et la rétention J1 / J7 / J30 sont mesurés automatiquement par Unity, sans rien déclarer.

## Fiches des stores

- **App Store Connect** > Confidentialité de l'app : ajouter « Données d'utilisation > Interactions avec le produit » et « Identifiants > ID de l'appareil », usage « Analyse », non liés à l'identité, sans suivi.
- **Google Play Console** > Sécurité des données : ajouter « Activité dans l'application » et « ID de l'appareil ou autres ID », finalité « Analyse », collecte facultative (consentement).
