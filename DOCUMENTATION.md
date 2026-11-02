# 🥋 Documentation Complète - Système de Compétition Kyokushinkai (Open de France)

Bienvenue dans la documentation technique et fonctionnelle de la solution logicielle de gestion de compétition de **Karaté Kyokushinkai**.

---

## 📑 Sommaire
1. [Présentation Générale](#1-présentation-générale)
2. [Architecture Technique & Choix Technologiques](#2-architecture-technique--choix-technologiques)
3. [Arborescence du Projet](#3-arborescence-du-projet)
4. [Règles Métier & Algorithmes Kyokushin](#4-règles-métier--algorithmes-kyokushin)
   - [Tirage au Sort Kumité (Arbre Éliminatoire)](#tirage-au-sort-kumité-arbre-éliminatoire)
   - [Gestion du Chronomètre & Prolongations (Encho-sen)](#gestion-du-chronomètre--prolongations-encho-sen)
   - [Déroulement des Katas en 2 Étapes](#déroulement-des-katas-en-2-étapes)
5. [Authentification, Rôles & Sécurité](#5-authentification-rôles--sécurité)
6. [Guide des Écrans & Interfaces](#6-guide-des-écrans--interfaces)
   - [Page de Connexion (`/login`)](#page-de-connexion-login)
   - [Affichage Téléviseur Tatami (`/tatami/{id}/tv`)](#affichage-téléviseur-tatami-tatamiidtv)
   - [Table de Marque (`/tatami/{id}/controller`)](#table-de-marque-tatamiidcontroller)
   - [Écran Public & Coachs (`/` et `/live`)](#écran-public--coachs--et-live)
   - [Arbres & Tableaux de Compétition (`/brackets`)](#arbres--tableaux-de-compétition-brackets)
   - [Administration & Import Excel (`/admin`)](#administration--import-excel-admin)
7. [Format du Fichier Excel d'Import](#7-format-du-fichier-excel-dimport)
8. [Commandes de Lancement & d'Exécution](#8-commandes-de-lancement--dexécution)
9. [Guide de Déploiement Gratuit sur Microsoft Azure](#9-guide-de-déploiement-gratuit-sur-microsoft-azure)

---

## 1. Présentation Générale

Cette application a été conçue sur-mesure pour répondre aux contraintes réelles de l'organisation d'une compétition de karaté Kyokushinkai :
- **Multi-tatamis simultanés** (Kumité et Kata).
- **Affichage sur téléviseurs haute lisibilité** avec chronomètre couleur et signal sonore automatique.
- **Accès public en temps réel** pour permettre aux spectateurs, coachs et compétiteurs de suivre les combats en cours et les combats à venir (échauffement).
- **Import automatisé depuis Excel** avec génération intelligente des tirages.
- **Hébergement économique / gratuit** optimisé pour Microsoft Azure (App Service ou Container Apps).

---

## 2. Architecture Technique & Choix Technologiques

- **Langage / Framework :** C# / .NET 10 (compatible .NET 8 / 9).
- **Interface Utilisateur :** Blazor Web App (mode `InteractiveServer`).
  - *Avantage majeur :* communication bidirectionnelle native via WebSockets (SignalR). Toute action effectuée à la table de marque (marquer un point, pause du chrono, ajout d'une prolongation) est répercutée en moins de **10 millisecondes** sur les écrans TV et les téléphones du public sans aucun rechargement de page.
- **Moteur Audio :** Web Audio API (`tournament-audio.js`) pour synthétiser directement le son du buzzer et du gong dans le navigateur, sans dépendance à des fichiers MP3 distants.
- **Base de Données & Persistance :** 
  - **Entity Framework Core** avec **SQLite** par défaut (`tournament.db`), ne nécessitant aucun serveur tiers payant et idéal pour un hébergement gratuit sur Azure App Service (dans `/home/site/tournament.db` sur disque persistant).
  - **Persistance en temps réel :** chaque action (attribution d'un Waza-ari/Ippon, avertissement, pénalité, prolongation, saisie des notes de juges Kata, import Excel) est immédiatement persistée en base de données.
  - **Tolérance aux pannes :** si l'application ou le serveur redémarre en plein milieu de la compétition, l'état complet du tournoi est rechargé à l'identique depuis la base de données.
  - **Compatibilité Azure SQL Database :** vous pouvez basculer sur une base managée Azure SQL en modifiant simplement la chaîne de connexion `DefaultConnection` dans `appsettings.json`.
- **Traitement Excel :** `ClosedXML` (génération et lecture `.xlsx` ultra-rapide sans interop Office).
- **Tests :** xUnit pour la validation continue des tirages et du calcul des scores.

---

## 3. Arborescence du Projet

```text
Open-De-France/
├── OpenDeFrance.sln
├── Dockerfile                           # Image de conteneur optimisée pour le Cloud
├── docker-compose.yml                   # Lancement multi-conteneurs local
├── README.md                            # Fiche récapitulative
├── DOCUMENTATION.md                     # Documentation complète
│
├── src/
│   ├── OpenDeFrance.Core/               # Bibliothèque de Domaine (Entities, Enums, Interfaces)
│   │   ├── Enums/
│   │   │   └── TournamentEnums.cs       # BeltGrade, MatchStatus, CornerColor, MatchWinnerReason
│   │   ├── Models/
│   │   │   ├── Competitor.cs            # Fiche athlète (nom, club, grade, poids, âge)
│   │   │   ├── Category.cs              # Catégorie (durées, katas imposés, qualifiés)
│   │   │   ├── KumiteMatch.cs           # Match (chrono, waza-ari, ippon, pénalités, prolongations)
│   │   │   ├── KataPerformance.cs      # Passage kata (5 notes, notes éliminées, score retenu)
│   │   │   ├── Tatami.cs                # Gestion de la surface et match en cours
│   │   │   └── Tournament.cs            # Configuration générale du tournoi
│   │   └── Services/
│   │       ├── IKumiteBracketGenerator.cs
│   │       ├── KumiteBracketGenerator.cs # Algorithme d'éloignement club et rapprochement poids/grade
│   │       ├── IKataTournamentManager.cs
│   │       ├── KataTournamentManager.cs  # Règles de notation et qualification 2 étapes
│   │       ├── IExcelTournamentService.cs
│   │       └── ITournamentService.cs
│   │
│   ├── OpenDeFrance.Infrastructure/     # Données, Fichiers & Services d'Infrastructure
│   │   ├── Data/
│   │   │   └── TournamentDbContext.cs   # Contexte Entity Framework Core
│   │   └── Services/
│   │       ├── ExcelTournamentService.cs# Import/Export Excel via ClosedXML
│   │       └── TournamentService.cs     # Moteur d'état temps réel & horloge d'arrière-plan
│   │
│   └── OpenDeFrance.Web/                # Application Web Blazor
│       ├── Components/
│       │   ├── Layout/
│       │   │   ├── MainLayout.razor
│       │   │   ├── EmptyLayout.razor    # Layout plein écran pour les télés de tatami
│       │   │   └── NavMenu.razor        # Menu de navigation du tournoi
│       │   └── Pages/
│       │       ├── Home.razor           # Direct public multi-tatamis et prochains matchs
│       │       ├── Brackets.razor       # Arbres éliminatoires et tableaux de bord Katas
│       │       ├── TatamiTv.razor       # Écran TV haute visibilité pour chaque tatami
│       │       ├── TatamiController.razor # Table de marque & pupitre de contrôle
│       │       └── Admin.razor          # Import Excel, paramétrage des catégories et tirages
│       ├── wwwroot/
│       │   └── js/tournament-audio.js   # Synthétiseur de Gong & Buzzer Web Audio API
│       └── Program.cs                   # Injection de dépendances et configuration HTTP
│
└── tests/
    └── OpenDeFrance.Tests/              # Suite de tests unitaires xUnit
        ├── KumiteBracketTests.cs        # Tests de séparation de club & arbres
        ├── KataTournamentTests.cs       # Tests de calcul des notes olympiques & qualifications
        └── ExcelTournamentTests.cs      # Tests de génération et lecture Excel
```

---

## 4. Règles Métier & Algorithmes Kyokushin

### Ordre Officiel & Numérotation Chronologique Globale
L'ensemble des tirages et passages de la compétition suit une numérotation continue stricte (#1, #2, #3, ...) :
1. **Étape 1 : Tous les Katas 1er Tour** de toutes les catégories (Passages #1 à #N).
2. **Étape 2 : Tous les Katas 2e Tour (Finales)** de toutes les catégories (Passages #(N+1) à #M).
3. **Étape 3 : Tous les Combats Kumités organisés tour par tour** :
   - Tous les **32èmes de finale** de toutes les catégories
   - Tous les **16èmes de finale** de toutes les catégories
   - Tous les **8èmes de finale** de toutes les catégories
   - Tous les **Quarts de finale** de toutes les catégories
   - Toutes les **Demi-finales** de toutes les catégories
   - Les **Petites Finales (3e place)** des catégories concernées
   - Toutes les **Grandes Finales** de toutes les catégories

### Gestion de la Petite Finale (3e Place) & Positionnement Géométrique
- Certaines catégories intègrent une **petite finale** opposant les perdants des deux demi-finales pour déterminer la 3ème place sur le podium. D'autres catégories attribuent deux 3èmes places ex-aequo sans petite finale.
- L'option `HasBronzeMatch` est **activable / désactivable à tout moment** par catégorie depuis l'espace `/admin`.
- Dès qu'une demi-finale se termine, le perdant est automatiquement placé dans la Petite Finale.
- **Positionnement Géométrique dans l'Arbre de Tournoi :**
  - Dans la colonne finale, la **Grande Finale reste parfaitement centrée verticalement au milieu exact des deux demi-finales** (`top: 50%; transform: translateY(-50%)`), garantissant l'alignement naturel des branches de l'arbre.
  - La **Petite Finale (3e place)** est positionnée proprement en dessous, dans la zone inférieure dédiée, sans décaler la Grande Finale de son axe central.

### Affectation & Équilibrage des Combats par Tatami et par Jour
1. **Regroupement par Catégorie :** Lors de la génération des tirages, **l'ensemble des combats d'une même catégorie est affecté sur le même tatami**, afin de garantir une cohérence pour les combattants, coachs et arbitres.
2. **Respect des Disciplines :** Les tatamis sont typés par discipline (`Kumite` ou `Kata`). Les catégories Kumité sont réparties exclusivement sur les tatamis Kumité, et les passages Kata sur les tatamis Kata.
3. **Équilibrage Multi-Jours (Bin-Packing LPT) :** Si la compétition se déroule sur plusieurs jours (ex: Jour 1 et Jour 2), l'algorithme équilibre les tatamis **jour par jour**. Sur chaque journée, les catégories de la discipline sont triées par volume décroissant de rencontres et distribuées sur le tatami le moins chargé de ce jour.
4. **Bouton « ⚖️ Rééquilibrer les combats sur les tatamis » :** Accessible dans l'onglet Tatamis de l'écran d'administration, ce bouton réinitialise les affectations précédentes et recalcule une distribution parfaitement équitable (par jour et discipline), mettant instantanément à jour la base SQLite et tous les tableaux de bord.
5. **Flexibilité & Réaffectation Manuelle :** L'administrateur peut à tout moment choisir un tatami spécifique pour une catégorie via le menu déroulant (ou le remettre en mode `⚡ Auto (Équilibré)`). L'ajout, la suppression ou le changement de discipline d'un tatami déclenche également un rééquilibrage automatique.

### Bouton de Changement de Langue (Bilingue FR / EN)
- Un sélecteur de langue interactif (`LanguageSwitcher`) présent dans la barre supérieure et dans le menu latéral permet de basculer instantanément l'affichage entre le **Français 🇫🇷** et l'**Anglais 🇬🇧** sans rechargement de page.

> *Ce séquencement garantit à la fois que les épreuves techniques (Katas) ont lieu en début de compétition, et que les combattants bénéficient d'un temps de repos équitable et régulier entre chaque tour éliminatoire.*
L'algorithme implémenté dans [`KumiteBracketGenerator`](file:///C:/Works/Open-De-France/src/OpenDeFrance.Core/Services/KumiteBracketGenerator.cs) applique trois principes essentiels :
1. **Éloignement maximal des combattants du même pays et du même club :**
   - Une matrice de distance arborescente pénalise au maximum toute proximité entre compétiteurs d'une même nation ou d'un même club.
   - Deux combattants d'un même pays ou d'un même club ne peuvent **jamais s'affronter au 1er tour** (sauf impossibilité mathématique absolue) et sont dirigés en priorité vers des demi-tableaux opposés afin de ne se rencontrer qu'en finale.
   - En Kata, l'ordre de passage du 1er tour alterne les pays pour éviter les passages consécutifs d'athlètes d'une même nation.
2. **Équité sportive (Poids & Grade) :**
   - Pour les matchs initiaux, l'algorithme privilégie les oppositions entre combattants de niveau (grade) et de gabarit (poids) proches.
   - Les combattants les plus gradés sont répartis comme têtes de série naturelles.
3. **Gestion automatique des puissances de 2 (Byes / Exempts) :**
   - Si le nombre de participants n'est pas une puissance de 2 ($2, 4, 8, 16, 32, 64$), les combattants exempts passent automatiquement au tour suivant.
4. **Règle absolue : Aucun combat sans combattant au premier tour :**
   - Chaque combat du 1er tour comporte **au minimum un combattant** (jamais 0 combattant). Un combat comporte soit 2 combattants (affrontement direct), soit 1 seul combattant opposé à un Exempt (`(Exempt)`).
   - Les exempts (Byes) sont attribués de droit aux têtes de série les plus gradées et distribués aux positions officielles de l'arbre éliminatoire.
   - En mode administration, le Drag & Drop empêche toute opération qui laisserait un combat du 1er tour avec 0 combattant.

### Gestion du Chronomètre & Prolongations (Encho-sen)
- La durée réglementaire dépend de l'âge (ex: 1m30 pour les benjamins/minimes, 2m pour les cadets/juniors/seniors).
- **En cas d'égalité (*Hikiwake*) :** l'arbitre de table clique sur **➕ PROLONGATION (ENCHO-SEN)**.
  - Le chronomètre est instantanément réinitialisé à la durée de prolongation propre à la catégorie (ex: 1m ou 2m).
  - Un compteur de prolongation est incrémenté.
  - L'écran TV affiche un bandeau ambre clignotant : `1ère Prolongation (Encho-sen)` ou `2ème Prolongation (Sai-encho-sen)`.

### Déroulement des Katas en 2 Étapes
1. **1er Tour (Éliminatoire) :**
   - Tous les compétiteurs exécutent le **Kata imposé du Tour 1** (ex: *Pinan Sono Ichi* ou *Gekisai Dai* selon l'âge).
   - 5 juges attribuent chacun une note entière entre **55 et 100** (sans virgule, ex: 75 au lieu de 7.5).
   - **Formule Kyokushin :** La note la plus basse et la note la plus haute sont éliminées ; la note finale retenue est la somme des 3 notes intermédiaires (ex: $72 + 75 + 78 = 225$).
   - **Nombre de qualifiés configurable :** Par défaut les 4 premiers sont qualifiés, mais l'administrateur peut choisir librement le nombre de qualifiés pour le Tour 2 (ex: 3, 4, 5, 6 ou 8) depuis l'onglet Tirages ou Paramétrage des Catégories.
2. **2e Tour (Finale & Podium) :**
   - Les finalistes exécutent le **Kata imposé du Tour 2** (ex: *Pinan Sono Ni* ou *Saifa*).
   - **Ordre de passage inversé :** le 4e qualifié passe en premier, le 1er qualifié passe en dernier.
   - Attribution du podium final (🥇 1er, 🥈 2e, 🥉 3e, 4e).

---

## 5. Authentification, Rôles & Sécurité

L'application intègre un système d'authentification et de contrôle d'accès basé sur les rôles (**RBAC - Role-Based Access Control**), garantissant qu'un simple spectateur ne puisse pas modifier les scores ou le chronomètre.

### Matrice des Droits d'Accès

| Rôle | Direct Public (`/`, `/brackets`) | Écrans Tatami TV (`/tatami/{id}/tv`) | Régie Streamer (`/tatami/{id}/streamer`) | Chambre d'Appel (`/collector`) | Table de Marque (`/tatami/{id}/controller`) | Administration & Import (`/admin`) |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Public / Non connecté** | ✅ Lecture seule | ❌ Redirection `/login` | ❌ Redirection `/login` | ❌ Redirection `/login` | ❌ Redirection `/login` | ❌ Redirection `/login` |
| **Tv** | ✅ Lecture | ✅ Plein écran | ❌ Droits insuffisants | ❌ Droits insuffisants | ❌ Droits insuffisants | ❌ Droits insuffisants |
| **Streamer** | ✅ Lecture | ✅ Plein écran | ✅ Combats à venir (lecture seule) | ❌ Droits insuffisants | ❌ Droits insuffisants | ❌ Droits insuffisants |
| **Collector** | ✅ Lecture | ❌ Droits insuffisants | ❌ Droits insuffisants | ✅ Appel & pointage athlètes | ❌ Droits insuffisants | ❌ Droits insuffisants |
| **Table** | ✅ Lecture | ✅ Plein écran | ✅ Consultation | ✅ Consultation | ✅ Chrono, scores, katas | ❌ Droits insuffisants |
| **Admin** | ✅ Lecture | ✅ Plein écran | ✅ Consultation | ✅ Contrôle total | ✅ Contrôle total | ✅ Import Excel, tirages, reset |

> **Redirection automatique :** Toute tentative d'accès à une page protégée par un utilisateur non connecté redirige automatiquement vers la page de connexion (`/login?returnUrl=...`). Après authentification, l'utilisateur est automatiquement renvoyé vers la page initialement demandée. Si l'utilisateur est déjà connecté mais que son rôle est insuffisant, un écran d'alerte l'informe de ses privilèges avec un lien pour basculer de compte.

### Comptes Pré-configurés & Identifiants

Pour faciliter les tests locaux ou le paramétrage initial, 5 profils sont pré-configurés :

1. **👑 Administrateur Général (`Admin`)**
   - **Identifiant :** `admin`
   - **Mot de passe :** `admin123` *(ou `admin`, `kyokushin`)*
   - **Accès :** Accès absolu à tous les modules, régénération des tirages, paramétrage des catégories, réinitialisation de la compétition.
2. **⏱️ Table de Marque Tatami (`Table`)**
   - **Identifiant :** `table`
   - **Mot de passe :** `table123` *(ou `table`, `tatami`)*
   - **Accès :** Contrôle des chronos (démarrer/pause), attribution Waza-ari/Ippon/pénalités, prolongations Encho-sen, saisie des 5 notes Kata.
3. **📺 Écran Téléviseur Tatami (`Tv`)**
   - **Identifiant :** `tv`
   - **Mot de passe :** `tv123` *(ou `tv`, `ecran`)*
   - **Accès :** Affichage optimisé grand écran des tatamis pour les téléviseurs suspendus.
4. **🎬 Régie Streamer / Commentateurs (`Streamer`)**
   - **Identifiant :** `streamer`
   - **Mot de passe :** `streamer123` *(ou `streamer`, `stream`, `live`)*
   - **Accès :** Écran de régie vidéo en **lecture seule stricte** (`/tatami/{id}/streamer` et `/tatami/{id}/tv`). Visualisation en direct du combat/kata en cours avec chrono synchronisé, encart « À suivre » pour l'antenne, liste complète filtrable des rencontres à venir, sans possibilité d'altérer le chrono ou les scores.
5. **🏃 Chambre d'Appel / Collecteur (`Collector`)**
   - **Identifiant :** `collector`
   - **Mot de passe :** `collector123` *(ou `collector`, `appel`, `chambre`)*
   - **Accès :** Écran mobile simplifié (`/collector`) pour les personnes chargées de chercher les compétiteurs dans la zone d'échauffement : affichage grand format des numéros de dossard, tatamis de destination, coins (Aka/Shiro), clubs et bouton de pointage « En place au tatami ».

> **Astuce test rapide :** Sur la page de connexion `/login`, 5 boutons d'accès rapide **« Connexion 1-Clic »** permettent de tester instantanément chaque rôle sans avoir à saisir le mot de passe.

---

## 6. Guide des Écrans & Interfaces

| Écran | URL | Rôle Requis | Description |
|---|---|---|---|
| **Connexion Officiels** | `/login` | Public | Formulaire d'authentification avec sélection rapide des profils Admin, Table, Tv, Streamer, Collector. |
| **Direct Public** | `/` ou `/live` | Public | Vue multi-tatamis en temps réel et liste des prochains combats (pour l'échauffement des combattants). Recherche par nom ou club. |
| **Tableaux & Tirages** | `/brackets` | Public (Admin enrichi) | Arbres éliminatoires visuels Kumité et classements des deux tours Kata. **En mode Admin :** réorganisation des tirages au sort par **Drag & Drop** (icône ⠿) et sélecteurs directs pour changer le tatami de chaque rencontre. |
| **Écran Téléviseur Tatami** | `/tatami/{id}/tv` | `Admin`, `Table`, `Tv`, `Streamer` | Affichage plein écran (F11) géant avec chrono bicolore, coins AKA/SHIRO, points, fautes, prolongations et buzzer sonore automatique. |
| **Régie Streamer (Live)** | `/tatami/{id}/streamer` | `Admin`, `Table`, `Streamer` | Interface pour commentateurs et régie de streaming : combat/kata en cours en direct, encart du prochain combat à l'antenne, file d'attente complète avec filtres (jour, statut, recherche) et copie des infos de combat. **Lecture seule stricte :** aucun bouton de modification de chrono, score ou notes. |
| **Chambre d'Appel (Collector)** | `/collector` | `Admin`, `Table`, `Collector` | Interface mobile simplifiée pour les collecteurs de compétiteurs : affichage des prochains combattants par tatami avec dossard géant, club, coin et bouton tactile pour valider la mise en place. |
| **Table de Marque** | `/tatami/{id}/controller` | `Admin`, `Table` | Pupitre pour l'arbitre de table : démarrer/stopper le chrono (Espace), boutons de prolongation, attribution des points, saisie des notes Kata. **Tatamis Mixtes :** boutons de bascule en 1 clic entre Mode Kumité et Mode Kata. **Filtre par Jour :** affichage ciblé des combats du Jour 1 ou Jour 2. **Enchaînement automatique :** dès qu'un vainqueur est désigné, le tatami enchaîne sur la rencontre suivante. |
| **Administration & Tirages** | `/admin` | `Admin` | Import/Export Excel, configuration multi-jours, gestion des tatamis (`Kumité`, `Kata` ou `Les deux`). **Onglet Tirages :** modification interactive des arbres et ordre Kata. **Onglet Tatamis (Kanban) :** vue colonnes multi-jours, rééquilibrage automatique par LPT bin-packing, changement de jour en direct par combat. |

### Gestion Multi-Jours & Tatamis Polyvalents (Kumité, Kata, ou Les Deux)

1. **Disciplines de Tatami Polyvalentes :**
   - Chaque tatami peut être configuré comme :
     - **🥊 Kumité uniquement :** n'accueille que des combats.
     - **🥋 Kata uniquement :** n'accueille que des passages techniques.
     - **⚡ Les deux (Kumité &amp; Kata) :** tatami polyvalent (ex: Katas en matinée / Kumité l'après-midi, ou alternance selon les journées). L'algorithme d'équilibrage distribue équitablement les deux types d'épreuves sur ce tatami.
   - À la Table de Marque (`/tatami/{id}/controller`), l'arbitre dispose d'un sélecteur dédié `[🥊 Mode Kumité]` / `[🥋 Mode Kata]` pour changer d'interface en direct sans altérer la programmation du tournoi.

2. **Planning Multi-Jours & Numérotation Chronologique :**
   - L'ensemble des rencontres et passages techniques est ordonné chronologiquement par jour :
     - `Jour 1 : Katas P#1... -> Kumité #101...`
     - `Jour 2 : Katas P#201... -> Kumité #301...`
   - Le changement du jour d'un combat ou d'une catégorie régénère automatiquement les numéros de passage pour préserver une continuité parfaite.
   - Des filtres de jour `[Tous les jours] [📅 Jour 1] [📅 Jour 2]` sont disponibles sur l'ensemble des pages (Accueil, Admin Kanban, Répertoire des rencontres, Table de marque).

---

## 7. Format du Fichier Excel d'Import

L'application intègre un modèle type téléchargeable en un clic depuis `/admin` (bouton *📥 Modèle Excel (.xlsx)*) ou via l'API `/api/tournament/excel-template`.

Le classeur est structuré en **deux feuilles** afin de découpler les informations d'inscription des athlètes et la programmation des épreuves :

### Feuille 1 : « Compétiteurs » (Inscrits)
1. **Dossard** *(Optionnel, numérotation séquentielle automatique si vide)*
2. **Nom** (ex: `DUPONT`)
3. **Prénom** (ex: `Jean`)
4. **Pays** (ex: `France`, `Espagne`, `Japon`...)
5. **Sexe (M/F)** (ex: `M` ou `F`)
6. **Âge** (ex: `24`)
7. **Date Naissance** (ex: `2002-05-14`)
8. **Club** (ex: `Dojo Paris 11`)
9. **Grade** (ex: `Shodan`, `1er Kyu`, `2eme Kyu`, `3eme Kyu`, `4eme Kyu`, `5eme Kyu`, `6eme Kyu`, `7eme Kyu`, `8eme Kyu`, `9eme Kyu`, `10eme Kyu`, `11eme Kyu`)
10. **Poids (kg)** (ex: `73.5`)
11. **Kumite (O/N)** (ex: `O` ou `N`)
12. **Kata (O/N)** (ex: `O` ou `N`)
13. **Catégorie Kumite** *(Optionnel, auto-détectée selon âge/sexe/poids si vide)*
14. **Catégorie Kata** *(Optionnel, auto-détectée selon âge/sexe si vide)*

### Feuille 2 : « Catégories » (Programmation Jour, Tatami & Ordre par Catégorie)
Permet de définir avec précision le planning de chaque catégorie sans dupliquer ces informations sur chaque compétiteur :
1. **Catégorie** (ex: `Senior Hommes -75kg`, `Senior Hommes Kata`)
2. **Discipline** (`Kumite` ou `Kata`)
3. **Jour** (numéro du jour de compétition : `1`, `2`...)
4. **Tatami** (numéro du tatami assigné : `1`, `2`, `3`...)
5. **Ordre** (numéro d'ordre de passage : `1`, `2`, `3`...)

### Gestion de l'Ordre de Passage des Catégories

- Chaque catégorie possède un **numéro d'ordre de passage** (`OrderIndex`).
- Dans l'onglet **Catégories** de l'Administration (`/admin`) :
  - Un champ numérique permet de renseigner directement le rang de passage souhaité.
  - Deux boutons d'incrémentation tactile **▲ (Monter / plus tôt)** et **▼ (Descendre / plus tard)** permettent de réordonner les catégories en 1 clic.
- La numérotation chronologique continue globale (`ApplyChronologicalNumbering`) applique automatiquement cet ordre :
  - Les épreuves des catégories avec un ordre inférieur sont programmées et numérotées avant celles des catégories d'ordre supérieur.
  - L'ordre est immédiatement synchronisé sur les pupitres de tables de marque, la régie streamer et la chambre d'appel.

> **Rétrocompatibilité :** L'importateur accepte également les fichiers à feuille unique comportant des colonnes de planification spécifiques (`Jour Kumite`, `Tatami Kumite`, `Jour Kata`, `Tatami Kata`) ou les anciennes colonnes globales.

---

## 8. Optimisations Mobiles & Tablettes (Responsive Design)

L'ensemble de l'application a été optimisé pour une utilisation fluide sur **smartphones**, **tablettes** (iPad, tablettes tactiles des tables de marque) et **écrans larges** :
1. **Menu latéral Offcanvas tactile :**
   - Sur mobile et tablette (< 768px), le menu latéral se replie par défaut et s'ouvre sous la forme d'un volet coulissant (drawer) avec fond semi-transparent flouté.
   - La sélection d'un lien ferme automatiquement le menu.
2. **Table de Marque (`/tatami/{id}/controller`) tactile :**
   - Chronomètre dynamique avec taille de police fluide (`clamp`).
   - Grands boutons tactiles pour les arbitres de table (Démarrer/Arrêter Hajime/Yame, Waza-Ari, Ippon, Fautes, Prolongations).
   - Formulaire de saisie des 5 notes Kata réorganisé en grille adaptée pour les écrans tactiles étroits.
3. **Tableaux et Tirages (`/brackets`) :**
   - Défilement horizontal tactile fluide (`-webkit-overflow-scrolling: touch`) pour naviguer dans l'arbre éliminatoire sans déformation.
   - En-tête adaptatif pour la sélection de catégorie et les quotas de qualification.
4. **Écrans Tatami TV (`/tatami/{id}/tv`) :**
   - Typographie adaptative pour conserver la lisibilité du chrono et des noms des combattants même lors d'une consultation sur smartphone ou tablette en bord de tatami.

---

## 9. Commandes de Lancement & d'Exécution

### A. Lancement en Local (Développement)
Assurez-vous que le SDK .NET 10 (ou .NET 8+) est installé.

```powershell
# Cloner ou se positionner dans le répertoire du projet
cd C:\Works\Open-De-France

# Compiler la solution
dotnet build

# Exécuter tous les tests unitaires
dotnet test

# Démarrer l'application web
dotnet run --project src/OpenDeFrance.Web
```
L'application démarre et affiche les URL d'écoute (par défaut `http://localhost:5000` ou `https://localhost:5001`).

---

### B. Lancement via Docker / Docker Compose
Un [Dockerfile](file:///C:/Works/Open-De-France/Dockerfile) et un [docker-compose.yml](file:///C:/Works/Open-De-France/docker-compose.yml) sont préconfigurés.

```bash
# Construire et démarrer le conteneur en arrière-plan
docker compose up --build -d

# Vérifier les logs
docker compose logs -f

# Stopper le conteneur
docker compose down
```
L'application est immédiatement accessible sur `http://localhost:5000`.

---

## 10. Guide de Déploiement Gratuit sur Microsoft Azure

Comme vous disposez de **crédits Azure** ou souhaitez bénéficier des **niveaux gratuits (Free Tier)**, voici les 2 meilleures options :

### Méthode 1 : Azure App Service Linux (Recommandée)
1. **Connexion à Azure CLI :**
   ```bash
   az login
   ```

2. **Création du groupe de ressources et du plan d'hébergement :**
   ```bash
   # Créer un groupe de ressources
   az group create --name rg-kyokushin --location francecentral

   # Créer le plan d'hébergement (SKU B1 pour utiliser vos crédits avec performances optimales, ou F1 pour 100% gratuit)
   az appservice plan create \
       --name plan-kyokushin \
       --resource-group rg-kyokushin \
       --sku B1 \
       --is-linux
   ```

3. **Création de la WebApp :**
   ```bash
   az webapp create \
       --resource-group rg-kyokushin \
       --plan plan-kyokushin \
       --name kyokushin-tournament \
       --runtime "DOTNETCORE:10.0"
   ```

4. **Activer les WebSockets (Indispensable pour Blazor Server / SignalR) :**
   ```bash
   az webapp config set \
       --resource-group rg-kyokushin \
       --name kyokushin-tournament \
       --web-sockets-enabled true
   ```

5. **Publication et déploiement Zip :**
   ```powershell
   # Générer les fichiers de production
   dotnet publish src/OpenDeFrance.Web/OpenDeFrance.Web.csproj -c Release -o ./publish

   # Créer l'archive zip
   Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force

   # Déployer vers Azure
   az webapp deployment source config-zip \
       --resource-group rg-kyokushin \
       --name kyokushin-tournament \
       --src ./app.zip
   ```

Votre compétition sera en ligne sur : `https://kyokushin-tournament.azurewebsites.net`.

---

### Méthode 2 : Azure Container Apps (Quota Mensuel 100% Gratuit)
Azure Container Apps propose un quota gratuit perpétuel chaque mois (**180 000 vCPU-secondes** et **360 000 GiB-secondes**) :

```bash
# Création de l'environnement Container Apps
az containerapp env create \
    --name env-kyokushin \
    --resource-group rg-kyokushin \
    --location francecentral

# Déploiement direct depuis le code source
az containerapp up \
    --name app-kyokushin \
    --resource-group rg-kyokushin \
    --environment env-kyokushin \
    --source . \
    --ingress external \
    --target-port 8080
```
