# 🥋 Open de France Kyokushinkai - Gestion de Compétition & Affichage Tatami

Solution complète en **C# / .NET 10 & Blazor Interactive Server** conçue pour l'organisation et le déroulement d'une compétition de Karaté Kyokushinkai (Kumité et Kata).

---

## 📋 Fonctionnalités Clés

### 1. Kumité (Combats Kyokushinkai)
- **Arbres de tournois à élimination directe** (puissances de 2 avec gestion automatique des exempts / Byes).
- **Algorithme de tirage au sort intelligent** :
  - **Éloignement strict des combattants du même club/dojo** dans des sous-arbres distincts.
  - **Rapprochement des combattants proches en terme de grade (ceinture) et de poids** pour les oppositions du 1er tour.
- **Gestion des prolongations dynamiques (Encho-sen / Sai-encho-sen)** :
  - En cas d'égalité (*Hikiwake*), bouton dédié sur la table de marque pour ajouter une prolongation.
  - Réinitialisation instantanée du chronomètre à la durée de prolongation de la catégorie et affichage d'un badge clignotant sur l'écran TV.
- **Durée des combats paramétrable par catégorie d'âge** (ex: 60s pour poussins/pupilles, 90s pour benjamins/minimes, 120s pour cadets/seniors, prolongations adaptées).
- **Comptage des points Kyokushin** : Waza-ari (2 = Awase-Ippon automatique), Ippon, Chui (avertissements), Genten (2 = Hansoku / disqualification).

### 2. Katas (Compétition en 2 Étapes)
- **Katas imposés par catégorie d'âge et par tour** (ex: Pinan Sono Ichi au tour 1, Gekisai Dai au tour 2).
- **Notation officielle Kyokushin (5 juges)** :
  - Élimination de la note la plus haute et de la plus basse.
  - Somme des 3 notes médianes retenue comme score officiel.
- **Qualification pour la finale** :
  - Classement automatique du 1er tour.
  - Qualification des **4 ou 5 meilleurs** pour le 2e tour.
- **Ordre de passage du 2e tour inversé** (le 4e passe en premier, le 1er passe en dernier).
- **Podium final** (1er Or, 2e Argent, 3e Bronze, 4e).

### 3. Écrans Téléviseurs sur les Tatamis (`/tatami/{id}/tv`)
- Affichage plein écran (F11), contraste élevé et typographie géante visible de loin.
- Chronomètre couleur géant (Vert en cours, Ambre arrêté, Rouge clignotant <10s).
- Coins AKA (Rouge) et SHIRO (Blanc) avec noms, clubs, grades, poids, Waza-ari, Ippon, Chui et Genten.
- Signal sonore de fin de combat (Buzzer / Gong synthétisé via l'API Web Audio du navigateur, sans fichier externe).
- Affichage du combat suivant en bas de l'écran pour l'échauffement des athlètes.

### 4. Table de Marque (`/tatami/{id}/controller`)
- Contrôle en temps réel du chronomètre (Démarrer / Pause avec barre d'espace, +1s, -1s, +10s, -10s).
- Attribution des points et sanctions.
- Bouton de prolongation en 1 clic.
- Attribution du vainqueur avec qualification automatique dans l'arbre pour le tour suivant.

### 5. Écran Public & Coachs (`/` et `/live`)
- Vue multi-tatamis simultanée avec match en cours et chronomètre en temps réel.
- **Liste des prochains matchs** sur chaque tatami pour anticiper les montées sur le tapis.
- Barre de recherche par combattant ou par club.
- Consultation interactive des arbres de tournoi et des classements katas (`/brackets`).

### 6. Import Excel (.xlsx)
- Import direct des inscrits (Nom, Prénom, Sexe, Âge, Club, Grade, Poids, Inscription Kumité/Kata).
- Générateur intégré de fichier modèle Excel (`/api/tournament/excel-template`).

---

## 🚀 Démarrage Rapide en Local

### Prérequis
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Lancement
```bash
git clone <repo-url>
cd Open-De-France
dotnet run --project src/OpenDeFrance.Web
```

L'application est accessible sur :
- **Accueil / Live Public :** `http://localhost:5000` ou `https://localhost:5001`
- **Tableaux & Tirages :** `http://localhost:5000/brackets`
- **Administration & Import Excel :** `http://localhost:5000/admin`
- **Table de marque Tatami 1 :** `http://localhost:5000/tatami/1/controller`
- **Écran TV Tatami 1 :** `http://localhost:5000/tatami/1/tv`

---

## ☁️ Hébergement Gratuit & Déploiement Azure

Vous pouvez héberger cette application soit gratuitement, soit en utilisant vos **crédits Azure** :

### Option 1 : Azure App Service (Plan Gratuit F1 ou Plan Basic B1 avec vos crédits)
1. **Créer la ressource App Service via Azure CLI :**
   ```bash
   # Création du groupe de ressources
   az group create --name rg-kyokushin --location westeurope

   # Création du plan App Service (Free F1 ou B1 avec crédits)
   az appservice plan create --name plan-kyokushin --resource-group rg-kyokushin --sku B1 --is-linux

   # Création de la Web App
   az webapp create --resource-group rg-kyokushin --plan plan-kyokushin --name kyokushin-tournament --runtime "DOTNETCORE:10.0"

   # Activer les WebSockets pour Blazor Server / SignalR en temps réel
   az webapp config set --resource-group rg-kyokushin --name kyokushin-tournament --web-sockets-enabled true
   ```

2. **Déployer le code :**
   ```bash
   dotnet publish src/OpenDeFrance.Web/OpenDeFrance.Web.csproj -c Release -o ./publish
   cd publish
   zip -r ../app.zip .
   az webapp deployment source config-zip --resource-group rg-kyokushin --name kyokushin-tournament --src ../app.zip
   ```

### Option 2 : Docker / Azure Container Apps (Quota mensuel gratuit)
```bash
docker-compose up --build -d
```
Azure Container Apps offre chaque mois **180 000 secondes vCPU et 360 000 GiB de mémoire gratuitement**.

---

## 🧪 Tests Unitaires
Pour lancer la suite de tests automatisés :
```bash
dotnet test
```
Tests inclus :
- Séparation des compétiteurs du même club au 1er tour.
- Propagation des vainqueurs dans l'arbre binaire.
- Règle de notation Kata Kyokushin (élimination des notes min/max sur 5 juges).
- Qualification des 4/5 meilleurs au 2e tour Kata et inversion de l'ordre de passage.
- Génération et lecture du fichier modèle Excel.
