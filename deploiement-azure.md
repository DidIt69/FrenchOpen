# 🚀 Guide de déploiement Azure — Open de France Kyokushinkai

## 💶 Estimation des coûts

| Service | Tier | Coût/mois |
|---|---|---|
| **Azure App Service** | B1 (1 vCPU, 1.75 GB RAM) | ~12 € |
| **Azure SQL Database** | Basic (5 DTUs, 2 GB) | ~5 € |
| **Total estimé** | | **~17 €/mois** |

> [!TIP]
> **Option encore moins chère** : App Service **F1 (gratuit)** + SQL Basic (~5 €/mois).  
> Limitation F1 : 60 min CPU/jour, pas d'always-on, cold starts. Pour une compétition (usage ponctuel), ça peut suffire.

> [!NOTE]
> **Azure SQL Database Free Offer** : Sur un nouveau compte Azure, vous avez 12 mois gratuits incluant une SQL Database General Purpose Serverless 32 GB. Vérifiez votre éligibilité sur le portail.

---

## 🏗️ Étape 1 — Créer les ressources Azure (portail ou CLI)

### Via Azure CLI (recommandé)

```bash
# Connexion
az login

# Groupe de ressources
az group create --name rg-kyokushin --location francecentral

# Plan App Service B1 (Linux)
az appservice plan create \
  --name plan-kyokushin \
  --resource-group rg-kyokushin \
  --sku B1 \
  --is-linux

# App Service (nom unique mondial !)
az webapp create \
  --name open-de-france \
  --resource-group rg-kyokushin \
  --plan plan-kyokushin \
  --runtime "DOTNETCORE:10.0"

# Azure SQL Server
az sql server create \
  --name sql-kyokushin \
  --resource-group rg-kyokushin \
  --location francecentral \
  --admin-user adminkyokushin \
  --admin-password "VotreMotDePasse123!"

# Règle firewall : autoriser les services Azure
az sql server firewall-rule create \
  --resource-group rg-kyokushin \
  --server sql-kyokushin \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Base de données Basic
az sql db create \
  --resource-group rg-kyokushin \
  --server sql-kyokushin \
  --name kyokushin-db \
  --edition Basic \
  --capacity 5
```

---

## 🔑 Étape 2 — Configurer la connection string sur l'App Service

```bash
# La connection string est injectée comme variable d'environnement sécurisée
az webapp config connection-string set \
  --name open-de-france \
  --resource-group rg-kyokushin \
  --settings DefaultConnection="Server=tcp:sql-kyokushin.database.windows.net,1433;Initial Catalog=kyokushin-db;User ID=adminkyokushin;Password=VotreMotDePasse123!;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
  --connection-string-type SQLAzure
```

> [!IMPORTANT]
> Ne jamais stocker le mot de passe dans `appsettings.json` sur GitHub. Azure App Service injecte les connection strings comme variables d'environnement qui **surchargent** automatiquement `appsettings.json`.

---

## 🔄 Étape 3 — CI/CD avec GitHub Actions

### 3.1 Récupérer le Publish Profile

```bash
az webapp deployment list-publishing-profiles \
  --name open-de-france \
  --resource-group rg-kyokushin \
  --xml > publish_profile.xml
```

Ou depuis le **Portail Azure** :  
`App Service → open-de-france → Vue d'ensemble → Télécharger le profil de publication`

### 3.2 Ajouter le secret dans GitHub

1. Aller sur votre repo GitHub → **Settings → Secrets and variables → Actions**
2. Cliquer **New repository secret**
3. Nom : `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Valeur : coller le contenu XML du fichier publish_profile.xml

### 3.3 Workflow déjà créé

Le fichier [`.github/workflows/deploy-azure.yml`](file:///C:/Works/FrenchOpen/.github/workflows/deploy-azure.yml) est prêt.

**Modifier le nom de l'App Service** dans le workflow :
```yaml
env:
  AZURE_WEBAPP_NAME: "open-de-france"   # ← votre nom d'App Service
```

**À chaque `git push` sur `main`**, GitHub Actions va :
1. ✅ Restaurer les packages
2. ✅ Builder en Release
3. ✅ Lancer les 85 tests
4. ✅ Publier l'artefact
5. 🚀 Déployer sur Azure App Service

---

## ⚙️ Étape 4 — Configuration runtime .NET 10

> [!WARNING]
> .NET 10 peut ne pas encore être disponible comme runtime natif sur Azure App Service Linux. Dans ce cas, publier en **self-contained** :

```bash
# Modifier le workflow : remplacer la commande publish par :
dotnet publish src/OpenDeFrance.Web/OpenDeFrance.Web.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output ./publish
```

Ou utiliser un **conteneur Docker** (voir section optionnelle ci-dessous).

---

## 🗄️ Étape 5 — Première initialisation de la DB

L'application utilise `EnsureCreated()` au premier démarrage — les tables sont créées **automatiquement** à la première connexion. Aucune migration manuelle nécessaire.

---

## 🐳 Option alternative : Azure Container Apps (encore moins cher)

**Azure Container Apps** a un tier gratuit très généreux (180 000 vCPU-s/mois).

```dockerfile
# Dockerfile (à créer à la racine)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/OpenDeFrance.Web/OpenDeFrance.Web.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenDeFrance.Web.dll"]
```

**Coût Container Apps** : ~0-5 €/mois selon le trafic (facturation à l'usage).

---

## 📋 Récapitulatif des fichiers créés/modifiés

| Fichier | Rôle |
|---|---|
| `appsettings.json` | Placeholder connection string Azure SQL |
| `appsettings.Development.json` | SQLite local (dev sans Azure) |
| `Program.cs` | Auto-détection SQL Server vs SQLite selon la connection string |
| `.github/workflows/deploy-azure.yml` | Pipeline CI/CD GitHub Actions |

## ✅ Comportement selon l'environnement

| Environnement | Connection string | Provider |
|---|---|---|
| **Dev local** | `Data Source=tournament.db` | SQLite |
| **Azure (prod)** | `Server=tcp:...database.windows.net...` | SQL Server |
