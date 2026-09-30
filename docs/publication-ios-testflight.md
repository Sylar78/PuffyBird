# Publier PuffyBird sur TestFlight (iOS)

Le build iOS tourne entièrement sur GitHub : aucun Mac n'est nécessaire. Le workflow
`.github/workflows/ios-testflight.yml` fait trois choses :

1. Unity (image GameCI sous Linux) génère le projet Xcode, avec un numéro de build unique (le numéro d'exécution GitHub).
2. Un Mac GitHub archive le projet et le signe automatiquement grâce à une clé API App Store Connect.
3. Le même Mac envoie le build sur App Store Connect, où il apparaît dans TestFlight.

## À faire une seule fois

### 1. Compte Apple Developer

- S'inscrire à l'[Apple Developer Program](https://developer.apple.com/programs/) (99 $ par an). La validation peut prendre de quelques heures à deux jours.
- Relever le **Team ID** (10 caractères) dans [developer.apple.com/account](https://developer.apple.com/account), rubrique *Membership details*.

### 2. Identifiant et fiche de l'app

- Dans [Certificates, Identifiers & Profiles > Identifiers](https://developer.apple.com/account/resources/identifiers/list), cliquer sur **+**, choisir *App IDs* puis *App*, et saisir l'identifiant explicite `com.sylar78.puffybird`. Aucune capacité particulière n'est à cocher.
- Dans [App Store Connect > Apps](https://appstoreconnect.apple.com/apps), cliquer sur **+ > Nouvelle app** :
  - plateforme iOS ;
  - nom : PuffyBird (il doit être libre sur l'App Store, sinon en choisir un autre ; c'est le nom affiché, pas l'identifiant) ;
  - langue principale : français ;
  - identifiant de lot : `com.sylar78.puffybird` ;
  - SKU : `puffybird`.

### 3. Clé API App Store Connect

Dans [App Store Connect > Utilisateurs et accès > Intégrations > App Store Connect API](https://appstoreconnect.apple.com/access/integrations/api), onglet *Clés d'équipe* :

- générer une clé nommée « GitHub Actions » avec le rôle **Admin** (nécessaire pour que la signature automatique crée le certificat de distribution) ;
- télécharger le fichier `AuthKey_XXXXXXXXXX.p8`, qui n'est téléchargeable **qu'une fois** ;
- relever le **Key ID** et l'**Issuer ID** affichés sur la page.

### 4. Licence Unity

GameCI doit activer Unity sur la machine de build.

- **Licence Personal** : dans Unity Hub, être connecté avec une licence Personal active. Le fichier de licence se trouve sous Windows dans `C:\ProgramData\Unity\Unity_lic.ulf` (macOS : `/Library/Application Support/Unity/Unity_lic.ulf`). Son contenu complet va dans le secret `UNITY_LICENSE`. Si les deux secrets sont définis, le workflow privilégie cette licence et ignore `UNITY_SERIAL`.
- **Licence Personal, fichier `.ulf` absent** : une licence affichée dans Unity Hub ne suffit pas, le fichier n'est créé que lors d'une activation. Dans Unity Hub, ouvrir *Paramètres > Licences*, cliquer sur **Ajouter une licence** et choisir **Obtenir une licence Personal gratuite**, puis aller au bout des étapes. Le fichier apparaît ensuite dans `C:\ProgramData\Unity\` (dossier masqué : coller le chemin dans la barre d'adresse de l'Explorateur). Source : [documentation GameCI](https://game.ci/docs/github/activation). L'ancienne méthode par fichier `.alf` n'est plus prise en charge.
- **Licence Pro** : mettre le numéro de série dans `UNITY_SERIAL` ; `UNITY_LICENSE` est alors inutile.

### 5. Secrets GitHub

Dans le dépôt GitHub : **Settings > Secrets and variables > Actions > New repository secret**.

| Secret | Contenu |
|---|---|
| `UNITY_LICENSE` | Contenu du fichier `Unity_lic.ulf` (licence Personal) |
| `UNITY_EMAIL` | E-mail du compte Unity |
| `UNITY_PASSWORD` | Mot de passe du compte Unity |
| `UNITY_SERIAL` | Numéro de série, seulement pour une licence Pro |
| `APPLE_TEAM_ID` | Team ID Apple (étape 1) |
| `APPSTORE_KEY_ID` | Key ID de la clé API (étape 3) |
| `APPSTORE_ISSUER_ID` | Issuer ID (étape 3) |
| `APPSTORE_P8` | Contenu complet du fichier `.p8`, lignes `BEGIN` et `END` comprises |

## À chaque version

1. Sur GitHub, onglet **Actions > iOS TestFlight > Run workflow**. Le workflow doit se trouver sur la branche par défaut du dépôt pour apparaître dans la liste.
2. Compter environ 30 à 45 minutes : la première exécution est la plus longue, les suivantes réutilisent le cache Unity.
3. Une fois le workflow vert, Apple traite le build (10 à 30 minutes). Il apparaît ensuite dans App Store Connect, onglet **TestFlight**.
4. Ajouter des testeurs :
   - **internes** (jusqu'à 100 membres de l'équipe App Store Connect) : disponibles tout de suite ;
   - **externes** (jusqu'à 10 000, par e-mail ou lien public) : le premier build passe par une vérification rapide d'Apple (Beta App Review).
5. Les testeurs installent l'app **TestFlight** sur leur iPhone et acceptent l'invitation.

Le numéro de version affiché (`0.1.0`) est réglé dans `ProjectSetup.ConfigurePlayer` ; le numéro de build augmente seul à chaque exécution.

## Coût

Les minutes des Mac GitHub comptent dix fois plus que celles de Linux pour un dépôt **privé** (forfait gratuit : 2 000 minutes par mois, soit environ 200 minutes de Mac). Un envoi consomme environ 15 minutes de Mac. Pour un dépôt public, les minutes sont gratuites.

## En cas d'échec

| Symptôme | Cause probable |
|---|---|
| Échec à l'étape « Build Unity iOS », message de licence ou `Code 20110 (serial invalid)` | Licence Personal : secret `UNITY_LICENSE` incomplet ou expiré, réactiver dans Unity Hub et recopier le fichier. Licence Pro : vérifier le secret `UNITY_SERIAL`. |
| « No Account for Team » ou « No profiles for 'com.sylar78.puffybird' » | Clé API sans le rôle Admin, Team ID erroné, ou identifiant d'app non créé (étape 2). |
| « Invalid Bundle » ou refus à l'envoi | Fiche App Store Connect absente ou identifiant de lot différent de `com.sylar78.puffybird`. |
| « The bundle version must be higher » | Numéro de build déjà envoyé : relancer le workflow, qui prend un nouveau numéro. |
| Refus lié au SDK iOS | Apple exige le dernier SDK : le workflow prend le dernier Xcode stable ; vérifier que l'image `macos-latest` le contient. |
