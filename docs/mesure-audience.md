# Mesure d'audience (Unity Analytics)

Le jeu envoie des statistiques d'usage à Unity Analytics **seulement si le joueur a accepté** sur l'écran de consentement (premier lancement, puis Paramètres > Confidentialité). S'il refuse ou retire son accord, la collecte s'arrête.

Le package **Analytics** (`com.unity.services.analytics` 6.3.0) est déclaré dans `Packages/manifest.json` : Unity l'installe tout seul à l'ouverture du projet, et `Editor/PackageDefines` active le code (`Assets/PuffyBird/Monetization/UnityAnalyticsMetrics.cs`, symbole `PUFFYBIRD_ANALYTICS`). Avec Unity 6.2 et plus, l'accord du joueur est transmis au module UnityConsent de Unity (`EndUserConsent`), qui démarre ou arrête la collecte.

Les plantages et exceptions des joueurs remontent dans **Unity Cloud > Diagnostics** (rapport de plantages activé par `Editor/UnityCloudProject`), sans consentement : ce sont des données techniques, sans identifiant publicitaire. En complément, gratuits et déjà actifs : **Android vitals** (Play Console > Qualité) et **Plantages** de TestFlight / Xcode Organizer.

## Mise en place (une fois, dans Unity)

1. **Créer le projet Unity Cloud** : sur [cloud.unity.com](https://cloud.unity.com), dans l'organisation qui porte déjà LevelPlay, ouvrir (ou créer) le projet « PuffyBird ». Noter son **Project ID** (Projects > PuffyBird > Settings, de la forme `a1b2c3d4-…`) et l'**Organization ID** (Administration > Organization).
2. **Renseigner ces deux valeurs** dans `Assets/PuffyBird/Scripts/Editor/UnityCloudProject.cs` (`ProjectId`, `OrganizationId`), ou les donner à Claude. Le dossier `ProjectSettings/` n'est pas versionné : c'est ce fichier qui relie les builds de la CI au projet. Tant que `ProjectId` est vide, pas de mesure ni de rapport de plantages.
3. **Activer Analytics et Diagnostics** dans le tableau de bord du projet (cloud.unity.com > Analytics > Get started, puis Diagnostics).
4. **Créer les événements** dans le tableau de bord (cloud.unity.com > Analytics > Event Manager). Unity refuse les événements qui n'y sont pas déclarés :

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
