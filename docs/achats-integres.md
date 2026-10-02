# Achats intégrés : « Sans pub » et oiseaux payants

Tout est **non consommable** (acheté une fois, gardé pour toujours, restauré sur un nouvel appareil) et **cosmétique** : aucun achat ne change la difficulté.

| ID du produit (identique sur les deux stores) | Contenu | Prix conseillé |
|---|---|---|
| `fr.puffybird.app.noads` | Retire la bannière publicitaire | 1,99 € |
| `fr.puffybird.app.skin.ninja` | Oiseau NINJA (bandeau rouge) | 0,99 € |
| `fr.puffybird.app.skin.robot` | Oiseau ROBOT (métal, antenne) | 0,99 € |
| `fr.puffybird.app.skin.galaxy` | Oiseau GALAXY (violet pailleté) | 0,99 € |

Dans le jeu : bouton **OISEAUX** de l'écran titre (oiseaux, avec leur prix et **ACHETER**), et dans **PARAMÈTRES** les lignes **SUPPRIMER LES PUBS** et **RESTAURER LES ACHATS** (exigée par Apple). Les oiseaux payants et ces lignes n'apparaissent que si la boutique est disponible.

## 1. Installer le package Unity IAP (une fois, sur le PC)

1. Unity > **Window > Package Manager** > **Unity Registry** > **In App Purchasing** (version 5.x) > **Install**.
2. Rien d'autre à régler : à la recompilation, `Editor/PurchasingDefine` active le code de la boutique (symbole `PUFFYBIRD_IAP`). Sans le package, le jeu compile et tourne sans boutique.
3. Envoyer sur GitHub (Upload files) les deux fichiers modifiés : `Packages/manifest.json` et `Packages/packages-lock.json`. Les builds de la CI activeront alors les achats.

## 2. App Store Connect (iOS)

1. **Accords, taxes et banque** : accepter l'accord **Applications payantes** et renseigner compte bancaire et formulaires fiscaux. Sans cela, les produits restent « Prêt à soumettre » et l'achat échoue.
2. App PuffyBird > **Monétisation > Achats intégrés** > **+** > type **Non consommable**, pour chacun des 4 produits :
   - Nom de référence (ex. « Sans pub »), **ID du produit** exactement comme dans le tableau.
   - **Prix** : 1,99 € ou 0,99 €.
   - Localisation française (et anglaise) : nom affiché et description courte.
   - **Informations pour la révision** : une capture de l'écran OISEAUX ou PARAMÈTRES (touche C dans Unity).
3. À la prochaine soumission de version, ajouter les achats intégrés à la version (section « Achats intégrés et abonnements » de la page de version) : Apple les valide avec elle.
4. Tester avec un compte **Sandbox** (Utilisateurs et accès > Sandbox) sur un build TestFlight : aucun débit réel.

## 3. Play Console (Android)

1. **Configuration > Profil de paiement** : créer le profil marchand (une fois par compte développeur).
2. App PuffyBird > **Monétiser avec Play > Produits > Produits intégrés** > **Créer un produit**, pour chacun des 4 produits : ID exact du tableau, nom, description, prix, puis **Activer**.
   - La page n'est accessible qu'après l'envoi d'un bundle contenant la bibliothèque de facturation, donc après un build fait avec le package IAP installé (étape 1).
3. Tester : **Paramètres > Tests de licence** > ajouter les comptes Google des testeurs (achats de test sans débit), puis installer la version de test fermé.

## Fonctionnement

- Au lancement, la boutique se connecte, charge les prix localisés et récupère les achats passés.
- Un achat est enregistré sur l'appareil (`puffybird.owned`) avant d'être confirmé auprès du store : s'il est interrompu, il revient au lancement suivant. Les achats restent disponibles hors ligne.
- Un remboursement Apple retire le produit. Pas de vérification serveur des reçus : pour des achats cosmétiques à 0,99 €, ce n'est pas nécessaire.
