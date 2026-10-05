# Classement, succès et partage du score

Le jeu affiche un bouton trophée sur l'écran titre (en haut à gauche) et deux boutons sur l'écran de fin : **SHARE** (partager son score) et **RANKING** (classement).

- **iOS** : Game Center. Le code est prêt ; il reste à créer le classement dans App Store Connect (étape 1).
- **Android** : Play Games Services v2. Le code est prêt, mais la bibliothèque Play Games n'est ajoutée au build qu'une fois les deux identifiants renseignés dans `Assets/PuffyBird/Scripts/Runtime/Social/SocialIds.cs` (étape 2). Sans eux, le bouton RANKING n'apparaît pas sur Android.
- **Partage** : rien à configurer. Le message (« J'ai fait 12 points à PuffyBird ! Tu fais mieux ? » + lien du site, dans la langue du jeu) s'ouvre dans la feuille de partage du téléphone (WhatsApp, SMS, Instagram…), accompagné d'une **capture de l'écran de fin** (sans les boutons). Sur Android, l'image est ajoutée à la galerie dans `Pictures/PuffyBird` (Android 10 ou plus) ; en dessous, seul le texte est partagé.
- **Succès** : 9 succès (étape 3), débloqués en jouant. Un succès gagné hors connexion est envoyé à la prochaine connexion, jamais deux fois. Sans les succès créés côté Apple ou Google, le jeu fonctionne normalement : ils sont simplement refusés par la plateforme.

## 1. Game Center (iOS)

1. [App Store Connect](https://appstoreconnect.apple.com) > **Apps** > PuffyBird > onglet **Services** (ou **Fonctionnalités** selon l'interface) > **Game Center**.
2. **Classements** > **+** > **Classement classique** :
   - Nom de référence : `Meilleur score`
   - **ID du classement : `puffybird.best`** (exactement, c'est celui du code)
   - Format du score : **Entier**
   - Envoi du score : **Meilleur score**, ordre de tri **Du plus haut au plus bas**
   - Plage de scores : 0 à 9999 (facultatif)
   - Localisation : ajouter **Français** (nom « Meilleur score », format « points ») puis **Anglais** (« Best score », « points »).
3. Sur la page de la **version** de l'app (onglet Distribution), cocher **Game Center** et ajouter le classement `puffybird.best`. Il sera validé avec la prochaine version soumise.

Le classement fonctionne tout de suite en TestFlight, avant la validation d'Apple. Au premier tap sur le trophée, Game Center propose de se connecter. Si le joueur a refusé une fois, iOS ne repropose pas la connexion : le bouton ouvre alors les Réglages.

## 2. Play Games Services (Android)

À faire dans la [Play Console](https://play.google.com/console), app PuffyBird :

1. **Développer** (ou **Croissance**) > **Services de jeux Play** > **Configuration et gestion** > **Configuration**.
2. **Non, mon jeu n'utilise pas encore les API Google** > nom `PuffyBird` > **Créer**.
3. **Identifiants** > **Ajouter des identifiants** > type **Android** :
   - La console propose de créer un projet Google Cloud et l'**écran de consentement OAuth** : suivre ses liens (type « Externe », nom PuffyBird, e-mail d'assistance, puis **Publier**).
   - Créer un **ID client OAuth** de type Android, nom de package `fr.puffybird.app`, empreinte **SHA-1 de la clé de signature de l'appli** : Play Console > **Tester et publier** > **Configuration** > **Intégrité de l'appli** > **Signature de l'appli** > « Certificat de la clé de signature d'application » > SHA-1.
   - Ajouter un second identifiant Android avec le SHA-1 du **certificat de la clé d'importation** (même page), pour les builds que vous installez vous-même.
4. **Classements** > **Créer un classement** : nom `Meilleur score`, format **Numérique**, ordre **Plus élevé = meilleur**. Copier son **ID** (il commence par `CgkI`).
5. **Testeurs** : ajouter les comptes Google des testeurs (tant que la configuration n'est pas publiée, seuls eux voient le classement).
6. Copier l'**ID du projet** affiché en haut de la page de configuration (un nombre de 12 chiffres environ).
7. Envoyer ces deux valeurs à Claude, ou les mettre dans `SocialIds.cs` :

   ```csharp
   public const string PlayGamesAppId = "123456789012";
   public const string PlayGamesLeaderboard = "CgkI...";
   ```

8. Relancer le workflow **Android Google Play**. Le build ajoute alors la bibliothèque Play Games et l'ID du projet au manifeste.
9. Une fois le test validé : **Services de jeux Play** > **Publier** pour ouvrir le classement à tous.

Au lancement, Play Games connecte le joueur automatiquement (petite bannière « Connecté en tant que… »). Sinon, la connexion est proposée au tap sur le trophée.

## 3. Succès (Game Center et Play Games)

Les 9 succès, leurs conditions et les icônes prêtes à envoyer (512 × 512, dans les fichiers du projet : `app-store/succes/`, regénérables par `python3 tools/achievements/make_icons.py`) :

| ID Game Center | Icône | Titre (FR / EN) | Condition | Points |
|---|---|---|---|---|
| `puffybird.ach.score10` | `score10.png` | Médaille de bronze / Bronze medal | 10 points dans une partie | 10 |
| `puffybird.ach.score20` | `score20.png` | Médaille d'argent / Silver medal | 20 points dans une partie | 20 |
| `puffybird.ach.score30` | `score30.png` | Médaille d'or / Gold medal | 30 points dans une partie | 30 |
| `puffybird.ach.score40` | `score40.png` | Médaille de platine / Platinum medal | 40 points dans une partie | 50 |
| `puffybird.ach.star` | `star.png` | Étoile filante / Shooting star | Attraper une étoile de vitesse | 10 |
| `puffybird.ach.closecalls` | `closecalls.png` | Sang-froid / Cool nerves | Frôler 5 tuyaux dans une même partie | 20 |
| `puffybird.ach.streak7` | `streak7.png` | Une semaine de vol / A week in flight | Jouer 7 jours de suite | 20 |
| `puffybird.ach.streak30` | `streak30.png` | Phénix éternel / Eternal phoenix | Jouer 30 jours de suite (débloque aussi le phénix LUMIÈRE) | 50 |
| `puffybird.ach.daily` | `daily.png` | Défi relevé / Challenge accepted | Lancer le défi du jour | 10 |

Total : 200 points (Apple en accepte 1000 par app). Aucun n'est « caché ».

### Game Center (iOS)

App Store Connect > PuffyBird > **Services** > **Game Center** > **Succès** > **+** : pour chacun, l'**ID** exact du tableau ci-dessus, le nom de référence, les points, **Succès masqué : non**, **Réalisable plusieurs fois : non**, l'icône, puis une localisation en français et en anglais (titre, description avant et après avoir été obtenu). Ajouter les succès à la version comme le classement (étape 1). Ils fonctionnent tout de suite en TestFlight.

### Play Games (Android)

Play Console > **Services de jeux Play** > **Configuration et gestion** > **Succès** > **Ajouter un succès** : nom, description, icône, points (multiples de 5, 1000 au total au maximum), état initial **Visible**, type **Standard**. Chaque succès reçoit un ID `CgkI…`. Les renseigner, dans l'ordre du tableau, dans `SocialIds.PlayGamesAchievements` (un ID vide = succès non envoyé) :

```csharp
public static readonly string[] PlayGamesAchievements = { "CgkI...", "CgkI...", /* 9 valeurs, dans l'ordre du tableau */ };
```

Le classement doit déjà être configuré (étape 2) : sans `PlayGamesAppId` et `PlayGamesLeaderboard`, Play Games n'est pas inclus dans le build.
