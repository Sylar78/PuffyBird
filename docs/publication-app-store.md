# Publier PuffyBird sur l'App Store

Suite de `publication-ios-testflight.md`. Un build envoyé sur TestFlight est déjà dans App Store Connect : c'est ce même build que l'on soumet à Apple, sans rien recompiler. Les libellés de menus d'App Store Connect changent parfois ; se fier aux intitulés proches.

## 1. Avant de soumettre

| À préparer | Pourquoi |
|---|---|
| **URL de politique de confidentialité** | Obligatoire. Le jeu affiche des pubs (LevelPlay, ironSource, Unity Ads) qui collectent l'identifiant publicitaire. Une page GitHub Pages suffit. |
| **URL d'assistance** | Obligatoire. Une page avec une adresse e-mail de contact suffit. |
| **Captures d'écran** | iPhone 6,9 pouces (1320 × 2868 ou 1290 × 2796), de 1 à 10 images. Le projet cible aussi l'iPad (`iPhoneAndiPad` dans `ProjectSetup`) : il faut alors aussi des captures iPad 13 pouces (2064 × 2752). Passer en iPhone seul supprime cette obligation. |
| **Version** | Le numéro de version de la fiche App Store doit être celui du build (`bundleVersion`, actuellement **0.1.0**). Pour sortir en 1.0.0, changer la version puis refaire un build. |
| **Build validé sur TestFlight** | Jouer une vraie partie sur iPhone avec le build choisi. |

## 2. Remplir la fiche dans App Store Connect

Dans **Apps > PuffyBird** :

1. **Informations sur l'app** : sous-titre (30 caractères), catégorie **Jeux**, sous-catégories **Arcade** et **Occasionnel**, droits sur le contenu (aucun contenu tiers), **classification par âge** (questionnaire : pas de violence réaliste ni de contenu sensible ; la présence de pubs se déclare dans le questionnaire).
2. **Tarifs et disponibilité** : gratuit, pays de diffusion.
3. **Confidentialité de l'app** : URL de la politique, puis le questionnaire sur les données collectées. Avec les SDK de pub, déclarer au minimum :
   - Identifiants (identifiant de l'appareil / publicitaire), Données d'utilisation (interactions avec les pubs), Diagnostics.
   - Finalité : **publicité tierce** ; données utilisées pour le **suivi** (le jeu demande l'autorisation ATT).
   - Se référer aux pages « App Store privacy » des réseaux actifs (ironSource / LevelPlay, Unity Ads) pour la liste exacte.
4. **Conformité de l'exportation** : déjà réglée dans le build (`ITSAppUsesNonExemptEncryption = false`).
5. **Statut de commerçant (UE, Digital Services Act)** : obligatoire pour diffuser dans l'UE. Une app monétisée par la pub relève en général du statut **commerçant** ; l'adresse et le téléphone déclarés sont alors publiés sur la fiche.

## 3. Préparer la version et soumettre

Dans l'onglet **Distribution**, version **0.1.0** (ou celle du build) :

1. Captures d'écran, texte promotionnel, description, mots-clés (100 caractères), URL d'assistance, copyright. Les textes en français, anglais, espagnol, allemand et portugais sont prêts dans `docs/fiches-stores-multilingues.md` (ajouter chaque langue dans la fiche, via le menu des langues de la version).
2. **Build** : choisir le build TestFlight (par exemple le 9).
3. **Informations pour la vérification** : coordonnées, pas de compte de démo (pas de connexion), note pour le vérificateur (« jeu d'arcade, un tap pour voler ; bannière de pub uniquement sur l'écran titre et l'écran de fin »).
4. **Publication de la version** : manuelle (conseillé pour choisir le jour) ou automatique après validation.
5. **Ajouter pour vérification**, puis **Soumettre**. La vérification prend en général de 1 à 3 jours.

## 4. Risques de refus à anticiper

- **Guideline 4.3 (spam) et 4.1 (copies)** : Apple a refusé beaucoup de clones de Flappy Bird. Mettre en avant ce qui est original dans la description et les captures : neuf décors, défi du jour, tuyaux mobiles, étoiles de vitesse, rendu 2.5D. Ne jamais employer « Flappy » dans le nom, les mots-clés ou la description.
- **Pubs** : la bannière ne doit jamais couvrir le jeu (c'est déjà la règle de `AdPolicy`).
- **ATT** : le texte d'autorisation doit expliquer l'usage (déjà ajouté par `IosPostBuild`, traduit dans les cinq langues du jeu) ; ne pas bloquer le jeu en cas de refus.
- **Consentement RGPD** : le jeu envoie `SetGDPRConsent(false)` faute d'écran de consentement. Apple ne le vérifie pas, mais un écran de consentement (CMP) sera nécessaire pour des pubs personnalisées en Europe.

## 5. Mises à jour

Chaque nouvelle version : incrémenter la version (`bundleVersion`), lancer le workflow **iOS TestFlight**, tester, puis créer une nouvelle version dans App Store Connect et y attacher le build. Le numéro de build (`github.run_number`) augmente tout seul.
