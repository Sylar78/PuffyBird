# Classement en ligne et partage du score

Le jeu affiche un bouton trophée sur l'écran titre (en haut à gauche) et deux boutons sur l'écran de fin : **SHARE** (partager son score) et **RANKING** (classement).

- **iOS** : Game Center. Le code est prêt ; il reste à créer le classement dans App Store Connect (étape 1).
- **Android** : Play Games Services v2. Le code est prêt, mais la bibliothèque Play Games n'est ajoutée au build qu'une fois les deux identifiants renseignés dans `Assets/PuffyBird/Scripts/Runtime/Social/SocialIds.cs` (étape 2). Sans eux, le bouton RANKING n'apparaît pas sur Android.
- **Partage** : rien à configurer. Le message (« J'ai fait 12 points à PuffyBird ! Tu fais mieux ? » + lien du site) s'ouvre dans la feuille de partage du téléphone (WhatsApp, SMS, Instagram…).

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
