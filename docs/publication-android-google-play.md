# Publier PuffyBird sur Google Play

Le bundle Android (`.aab`) est construit par GitHub Actions (workflow **Android Google Play**), sans rien installer sur le PC. Les libellés de la Play Console changent parfois ; se fier aux intitulés proches.

## 1. Compte Google Play

1. Créer un compte développeur sur [play.google.com/console](https://play.google.com/console) (frais uniques de 25 $, vérification d'identité).
2. **Compte personnel créé après novembre 2023** : avant la production, Google exige un **test fermé avec au moins 12 testeurs inscrits pendant 14 jours d'affilée**. Prévoir ces testeurs (amis, famille) dès le début.
3. **Créer l'application** : nom « PuffyBird », langue par défaut français, type **Jeu**, **Gratuit**, accepter les déclarations.

## 2. Clé d'envoi (une seule fois)

Google signe l'app finale lui-même (« signature d'application par Play ») ; on signe seulement les envois avec une **clé d'envoi**. Chaque bundle envoyé doit porter cette même clé. On la confie ensuite à GitHub sous forme de **secrets** pour qu'il signe les envois.

### A. Créer la clé dans Unity

1. Ouvrir le projet, puis Edit > Project Settings > **Player** > onglet **Android** (icône robot) > déplier **Publishing Settings** > bouton **Keystore Manager**. Pas d'onglet Android : ajouter le module « Android Build Support » dans Unity Hub (Installs > roue dentée de la version du projet > **Add modules** > cocher **Android Build Support**, avec OpenJDK et Android SDK & NDK Tools), puis rouvrir le projet. L'onglet n'apparaît pas tant que le module manque : Player n'affiche alors que PC et Web.
2. En haut de la fenêtre, menu **Keystore…** > **Create New** > **Anywhere…** : enregistrer `puffybird-upload.keystore` **hors du dossier du projet** (par exemple `C:\Cles\`).
3. Choisir un **mot de passe du keystore** et le confirmer.
4. Partie « New Key Values » :
   - **Alias** : `puffybird` ;
   - **mot de passe de la clé** (il peut être le même que celui du keystore) ;
   - **Validity (years)** : 50 ;
   - **First and Last Name** : son nom ; les autres champs sont facultatifs.
5. Cliquer sur **Add Key**. Si Unity propose d'utiliser ce keystore pour le projet, répondre **Non** : c'est GitHub qui signe.
6. **Sauvegarder** le fichier `.keystore` et les deux mots de passe (gestionnaire de mots de passe, copie sur une clé USB ou un cloud personnel). Sans eux, plus de mise à jour possible sans passer par le support Google. Ne jamais mettre le fichier dans le dépôt (`*.keystore` est dans `.gitignore`).

Variante sans module Android (téléchargement de plusieurs Go évité) : installer un JDK (par exemple Eclipse Temurin 17), puis dans PowerShell :

```powershell
keytool -genkeypair -v -keystore C:\Cles\puffybird-upload.keystore -alias puffybird -keyalg RSA -keysize 2048 -validity 18250
```

`keytool` demande le mot de passe du keystore, puis le nom et l'organisation (seul le nom compte), puis confirme. Le mot de passe de la clé est alors le même que celui du keystore. On reprend ensuite à l'étape 6.

### B. Convertir la clé en texte

Dans PowerShell, en adaptant le chemin :

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\Cles\puffybird-upload.keystore")) | Set-Clipboard
```

Le texte de la clé est alors dans le presse-papiers ; rien ne s'affiche, c'est normal.

### C. Créer les 4 secrets dans GitHub

Page du dépôt **puffybird** > **Settings** > **Secrets and variables** > **Actions** > **New repository secret**, un secret à la fois. Respecter exactement les noms, en majuscules :

| Secret | Valeur |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | le texte copié à l'étape B (coller) |
| `ANDROID_KEYSTORE_PASS` | mot de passe du keystore (A.3) |
| `ANDROID_KEYALIAS_NAME` | `puffybird` (l'alias) |
| `ANDROID_KEYALIAS_PASS` | mot de passe de la clé (A.4) |

Les secrets Unity (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) sont déjà ceux du build iOS. S'il manque un des 4 secrets, le workflow s'arrête dès sa première étape en disant lequel.

## 3. Construire le bundle

GitHub > onglet **Actions** > **Android Google Play** > **Run workflow** (branche `claude/init-claude-md-jrskg2`). Compter 15 à 30 min. Le bundle `PuffyBird-N.aab` est téléchargeable en bas de la page du run, section **Artifacts** (archive zip). Le numéro de version (`versionCode`) est le numéro du run.

## 4. Premier envoi (à la main, obligatoire)

L'API Google Play refuse le tout premier bundle d'une app : il passe par la console.

1. Play Console > PuffyBird > **Tester et publier** > **Tests internes** > **Créer une release**.
2. Accepter la **signature d'application par Play**.
3. Déposer le `.aab`, donner un nom de version, **Enregistrer**, puis **Examiner la release** et lancer le déploiement.
4. Dans **Testeurs**, créer une liste d'adresses e-mail et partager le lien d'inscription.

## 5. Envois suivants automatiques (facultatif)

1. Dans [console.cloud.google.com](https://console.cloud.google.com) : créer un projet, activer **Google Play Android Developer API**, créer un **compte de service**, puis une **clé JSON** (téléchargée).
2. Play Console > **Utilisateurs et autorisations** > **Inviter de nouveaux utilisateurs** : l'adresse e-mail du compte de service, avec accès à PuffyBird et le droit de publier sur les pistes de test (et la production si voulu).
3. Secret GitHub `PLAY_SERVICE_ACCOUNT_JSON` : tout le contenu du fichier JSON.

Ensuite, **Run workflow** envoie directement sur la piste choisie (`internal` par défaut). Statut `draft` tant que l'app n'a jamais été publiée (la release est alors à valider dans la console), `completed` ensuite.

## 6. Fiche et déclarations (Play Console > Présence sur le Play Store, Règles de l'app)

| Élément | Contenu |
|---|---|
| Description courte (80 car.) | « Un tap pour voler, six décors, des tuyaux qui bougent : battez votre record ! » |
| Description complète | celle de l'App Store (même texte) |
| Icône 512 × 512, image de présentation 1024 × 500, captures téléphone (rapport 2:1 au plus) | fichiers prêts dans les fichiers du projet, `app-store/google-play/` |
| Catégorie | Jeux > Arcade |
| Politique de confidentialité | `https://sylar78.github.io/puffybird-site/` |
| Accès à l'app | toutes les fonctionnalités sans restriction |
| Annonces | **Oui, l'app contient des annonces** |
| Identifiant publicitaire | utilisé, pour la **publicité** |
| Classification du contenu | questionnaire IARC : jeu, pas de violence réaliste, pas de contenu sensible |
| Public cible | 13 ans et plus (comme la déclaration COPPA de LevelPlay) |
| Sécurité des données | collectées et partagées pour la **publicité** : ID de l'appareil ou autres ID, interactions dans l'app, diagnostics, localisation approximative ; chiffrées en transit ; pas de compte donc pas de demande de suppression |

## 7. Publicité sur Android

Chez LevelPlay, chaque plateforme est une app distincte. Tant que `AdIds` (bloc `UNITY_ANDROID`) est vide, la version Android tourne **sans pub** (et sans écran de consentement). Le code Android est prêt ; il ne manque que les deux identifiants.

1. [Tableau de bord LevelPlay](https://platform.ironsrc.com/) > **Apps** > **Add app** :
   - Plateforme **Android**, app pas encore publiée : nom `PuffyBird`, package `fr.puffybird.app` (on pourra lier la fiche Play plus tard).
   - **Coppa** : « Not directed » (13 ans et plus), comme l'app iOS.
   - Copier l'**App Key** (sous le nom de l'app).
2. **Ad units** > app Android > **Banner** : rafraîchissement **30 s**, comme sur iOS. Copier l'**ID de l'unité**.
3. Envoyer ces deux valeurs à Claude, ou les mettre dans `Assets/PuffyBird/Monetization/AdIds.cs`, bloc `#elif UNITY_ANDROID` :

   ```csharp
   public const string AppKey = "...";
   public const string BannerAdUnitId = "...";
   ```

4. Relancer le workflow **Android Google Play**.

Au premier lancement, le jeu demande le consentement aux pubs personnalisées (écran PRIVACY) ; la pub ne s'initialise qu'après la réponse. La permission `AD_ID` est ajoutée au manifeste par `Editor/AndroidPostBuild`, le SDK Android de LevelPlay par le résolveur de dépendances.

## 8. Mises à jour

Chaque nouveau build : relancer le workflow (le `versionCode` augmente tout seul). Pour changer le numéro de version affiché, modifier `bundleVersion`.
