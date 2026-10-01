# Publier PuffyBird sur Google Play

Le bundle Android (`.aab`) est construit par GitHub Actions (workflow **Android Google Play**), sans rien installer sur le PC. Les libellés de la Play Console changent parfois ; se fier aux intitulés proches.

## 1. Compte Google Play

1. Créer un compte développeur sur [play.google.com/console](https://play.google.com/console) (frais uniques de 25 $, vérification d'identité).
2. **Compte personnel créé après novembre 2023** : avant la production, Google exige un **test fermé avec au moins 12 testeurs inscrits pendant 14 jours d'affilée**. Prévoir ces testeurs (amis, famille) dès le début.
3. **Créer l'application** : nom « PuffyBird », langue par défaut français, type **Jeu**, **Gratuit**, accepter les déclarations.

## 2. Clé d'envoi (une seule fois)

Google signe l'app finale lui-même (« signature d'application par Play ») ; on signe seulement les envois avec une **clé d'envoi**.

1. Dans Unity : Edit > Project Settings > Player > onglet Android > **Publishing Settings** > **Keystore Manager**.
2. Keystore… > **Create New** > Anywhere : enregistrer `puffybird-upload.keystore` **hors du dossier du projet**, choisir un mot de passe.
3. Ajouter une clé : alias `puffybird`, mot de passe, validité 50 ans, nom ou organisation.
4. **Sauvegarder** ce fichier et les mots de passe (gestionnaire de mots de passe) : sans eux, plus de mise à jour possible sans passer par le support Google.
5. Convertir la clé en texte (PowerShell, en adaptant le chemin) :
   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\Cles\puffybird-upload.keystore")) | Set-Clipboard
   ```
6. Dans GitHub : Settings > Secrets and variables > Actions > **New repository secret**, créer :

| Secret | Valeur |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | le texte copié à l'étape 5 |
| `ANDROID_KEYSTORE_PASS` | mot de passe du keystore |
| `ANDROID_KEYALIAS_NAME` | `puffybird` (l'alias) |
| `ANDROID_KEYALIAS_PASS` | mot de passe de l'alias |

Les secrets Unity (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) sont déjà ceux du build iOS.

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

Chez LevelPlay, chaque plateforme est une app distincte. Tant que `AdIds` (bloc `UNITY_ANDROID`) est vide, la version Android tourne **sans pub**. Créer l'app Android dans le tableau de bord LevelPlay (même nom, package `fr.puffybird.app`) et une unité de bannière, puis renseigner l'App Key et l'ID de bannière dans `Assets/PuffyBird/Monetization/AdIds.cs`. La permission `AD_ID` est ajoutée au manifeste par `Editor/AndroidPostBuild`.

## 8. Mises à jour

Chaque nouveau build : relancer le workflow (le `versionCode` augmente tout seul). Pour changer le numéro de version affiché, modifier `bundleVersion`.
