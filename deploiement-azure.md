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

## 🔄 Étape 3 — CI/CD avec GitHub Actions (Service Principal)

> [!IMPORTANT]
> L'approche **Publish Profile** peut échouer avec l'erreur `Publish profile is invalid for app-name`.  
> La méthode **Service Principal** (ci-dessous) est plus robuste et recommandée.

### 3.1 Créer un Service Principal Azure

```bash
# Récupérer votre Subscription ID
az account show --query id -o tsv

# Créer le Service Principal avec droits Contributor sur le groupe de ressources
az ad sp create-for-rbac \
  --name "sp-kyokushin-deploy" \
  --role contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/rg-kyokushin \
  --sdk-auth
```

La commande retourne un JSON :
```json
{
  "clientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "clientSecret": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "subscriptionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "tenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  ...
}
```

### 3.2 Ajouter le secret dans GitHub

1. Aller sur votre repo GitHub → **Settings → Secrets and variables → Actions**
2. Cliquer **New repository secret**
3. Nom : **`AZURE_CREDENTIALS`**
4. Valeur : **coller le JSON complet** retourné par la commande ci-dessus

### 3.3 Vérifier le nom exact de l'App Service

```bash
# Lister vos App Services pour connaître le nom exact
az webapp list --query "[].name" -o tsv
```

Mettre à jour la variable dans [`.github/workflows/deploy-azure.yml`](file:///C:/Works/FrenchOpen/.github/workflows/deploy-azure.yml) :
```yaml
env:
  AZURE_WEBAPP_NAME: "open-de-france"   # ← doit correspondre EXACTEMENT au nom Azure
```

### 3.4 Pipeline — ce qui se passe à chaque `git push` sur `main`

```
git push origin main
      │
      ▼
┌─────────────────────────────┐
│  Job 1 : Build & Test       │
│  1. dotnet restore          │
│  2. dotnet build --Release  │
│  3. dotnet test (85 tests)  │
│  4. dotnet publish          │
│  5. Upload artefact         │
└─────────────┬───────────────┘
              │ si succès
              ▼
┌─────────────────────────────┐
│  Job 2 : Deploy             │
│  1. az login (SP)           │
│  2. azure/webapps-deploy    │
│  3. az logout               │
└─────────────────────────────┘
      │
      ▼
  https://open-de-france.azurewebsites.net ✅
```

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
| `appsettings.json` | `DatabaseProvider=SqlServer` uniquement — **pas de connection string** |
| `appsettings.Development.json` | `DatabaseProvider=Sqlite` + connection string SQLite locale |
| `Program.cs` | Détection explicite via `DatabaseProvider`, logs de démarrage détaillés |
| `.github/workflows/deploy-azure.yml` | Pipeline CI/CD GitHub Actions (Service Principal) |

## ✅ Comportement selon l'environnement

| Environnement | `DatabaseProvider` | Connection string | Provider utilisé |
|---|---|---|---|
| **Dev local** | `Sqlite` (appsettings.Development.json) | `Data Source=tournament.db` | SQLite |
| **Azure (prod)** | `SqlServer` (appsettings.json) | Injectée par Azure App Service | SQL Server |

## 🌐 WebSockets sur Azure App Service

> [!IMPORTANT]
> Azure App Service **désactive les WebSockets par défaut**. Sans activation manuelle, Blazor génère dans la console :
> ```
> Warning: Failed to connect via WebSockets, using the Long Polling fallback transport.
> ```
> Ce warning est **bénin** si Long Polling fonctionne — mais il génère une latence plus élevée pour les mises à jour temps-réel.

### Option A — Rester en Long Polling (simple, sans action Azure)
Le code actuel force `transport = 4` (Long Polling uniquement) dans [`App.razor`](file:///C:/Works/FrenchOpen/src/OpenDeFrance.Web/Components/App.razor). **Aucune action requise.** La mise à jour des scores a ~1-2 sec de délai, acceptable pour une compétition.

### Option B — Activer les WebSockets (meilleures performances)

**Via le Portail Azure :**
1. App Service → **Configuration** → onglet **Paramètres généraux**
2. **Web sockets** → **Activé**
3. Enregistrer → Redémarrer l'app

**Via Azure CLI :**
```bash
az webapp config set \
  --name open-de-france \
  --resource-group rg-kyokushin \
  --web-sockets-enabled true
```

**Puis dans [`App.razor`](file:///C:/Works/FrenchOpen/src/OpenDeFrance.Web/Components/App.razor), changer :**
```js
// Avant (Long Polling uniquement)
var transport = 4;

// Après (WebSocket + LongPolling fallback)
var transport = 1 | 4;
```

> [!NOTE]
> WebSockets nécessitent le tier **B1 minimum** (pas disponible sur F1 gratuit). En tier F1, restez en Long Polling.

---

## 🔑 Configuration requise dans Azure App Service

### Onglet "Application Settings"

| Nom | Valeur |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DatabaseProvider` | `SqlServer` |

### Onglet "Connection Strings"

| Nom | Valeur | Type |
|---|---|---|
| `DefaultConnection` | `Server=tcp:championships.database.windows.net,1433;Initial Catalog=FrenchOpen2027;User ID=...;Password=...;Encrypt=True;` | **Custom** |

> [!CAUTION]
> La connection string **ne doit PAS être dans `appsettings.json`** (sécurité). Elle est uniquement dans Azure App Service → Configuration → Connection Strings.

> [!IMPORTANT]
> Le type doit être **Custom** (pas SQLAzure, pas SQLServer). Avec le type Custom, Azure injecte la connection string comme variable d'environnement `CUSTOMCONNSTR_DefaultConnection`, automatiquement mappée à `ConnectionStrings:DefaultConnection` par ASP.NET Core.

## 🩺 Diagnostics de démarrage

Au démarrage, l'application affiche dans les logs (Azure App Service → Log stream) :

```
╔══════════════════════════════════════════════════════╗
║  ENVIRONMENT  : Production                           ║
║  DatabaseProvider : SqlServer                        ║
║  ConnStr trouvée  : OUI                              ║
║  ConnStr début    : Server=tcp:championships...      ║
╚══════════════════════════════════════════════════════╝
[DB] ✅ Utilisation de SQL SERVER
```

**Si vous voyez `SQLite` ou `NON`** → la connection string n'est pas injectée correctement par Azure.
