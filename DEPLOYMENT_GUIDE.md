# Leave Management System - Azure Deployment Guide

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Architecture Overview](#architecture-overview)
3. [Local Development Setup](#local-development-setup)
4. [Azure Resources Setup](#azure-resources-setup)
5. [Configuration](#configuration)
6. [Deployment Steps](#deployment-steps)
7. [Monitoring & Maintenance](#monitoring--maintenance)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

- Azure subscription (with appropriate permissions)
- .NET 8 SDK
- Azure CLI
- Visual Studio 2022 or VS Code with C# extension
- Git

---

## Architecture Overview

The Leave Management System uses the following Azure services:

```
┌─────────────────────────────────────────────────────────────┐
│                      Azure App Service                       │
│                 (Hosted Razor Pages App)                     │
└──────────────────┬──────────────────────────────────────────┘
                   │
        ┌──────────┼──────────┐
        │          │          │
        ▼          ▼          ▼
    ┌────────┐ ┌────────┐ ┌───────────┐
    │ Azure  │ │ Azure  │ │ Application
    │  SQL   │ │  Blob  │ │ Insights
    │Database│ │Storage │ │
    └────────┘ └────────┘ └───────────┘
        │          │
        └──────────┼──────────┘
                   │
                   ▼
            ┌────────────────┐
            │  Azure Key     │
            │  Vault         │
            └────────────────┘
```

---

## Local Development Setup

### 1. Clone the Repository
```powershell
git clone <repository-url>
cd LeaveManagement
```

### 2. Restore and Build
```powershell
dotnet restore
dotnet build
```

### 3. Run Local Database
```powershell
dotnet ef database update
dotnet run
```

The app will run at `https://localhost:5001` with mock authentication (user/123 or manager/123).

### 4. Run Tests
```powershell
cd ..\LeaveManagement.Tests
dotnet test
```

---

## Azure Resources Setup

### Step 1: Create Resource Group
```powershell
$resourceGroupName = "rg-leave-management"
$location = "eastus"

az group create --name $resourceGroupName --location $location
```

### Step 2: Create Azure SQL Database

#### 2.1 Create SQL Server
```powershell
$sqlServerName = "lm-sql-server-$(Get-Random)"
$adminLogin = "azureAdmin"
$adminPassword = "YourSecurePassword123!" # Change this!

az sql server create `
  --resource-group $resourceGroupName `
  --name $sqlServerName `
  --location $location `
  --admin-user $adminLogin `
  --admin-password $adminPassword
```

#### 2.2 Create Database
```powershell
$databaseName = "LeaveManagementDB"

az sql db create `
  --resource-group $resourceGroupName `
  --server $sqlServerName `
  --name $databaseName `
  --edition Basic
```

#### 2.3 Configure Firewall
```powershell
# Allow Azure services
az sql server firewall-rule create `
  --resource-group $resourceGroupName `
  --server $sqlServerName `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0

# Allow your IP (for local development)
$yourIp = "YOUR_PUBLIC_IP"
az sql server firewall-rule create `
  --resource-group $resourceGroupName `
  --server $sqlServerName `
  --name AllowMyIP `
  --start-ip-address $yourIp `
  --end-ip-address $yourIp
```

#### 2.4 Get Connection String
```powershell
az sql db show-connection-string `
  --admin-user $adminLogin `
  --server $sqlServerName `
  --name $databaseName `
  --client sqlserver
```

Update `appsettings.Production.json` with this connection string.

### Step 3: Create Azure Storage Account (Blob Storage)

```powershell
$storageAccountName = "lmstorage$(Get-Random -Maximum 10000)"

az storage account create `
  --resource-group $resourceGroupName `
  --name $storageAccountName `
  --location $location `
  --sku Standard_LRS `
  --kind StorageV2

# Create container
az storage container create `
  --account-name $storageAccountName `
  --name attachments `
  --public-access off

# Get connection string
az storage account show-connection-string `
  --resource-group $resourceGroupName `
  --name $storageAccountName
```

Update `appsettings.Production.json` with the Blob Storage connection string.

### Step 4: Create Azure Key Vault

```powershell
$keyVaultName = "kv-lm-$(Get-Random -Maximum 10000)"

az keyvault create `
  --resource-group $resourceGroupName `
  --name $keyVaultName `
  --location $location `
  --enable-rbac-authorization
```

### Step 5: Create Application Insights

```powershell
$appInsightsName = "ai-leave-management"

az monitor app-insights component create `
  --app $appInsightsName `
  --location $location `
  --resource-group $resourceGroupName `
  --application-type web

# Get instrumentation key
az monitor app-insights component show `
  --app $appInsightsName `
  --resource-group $resourceGroupName `
  --query instrumentationKey
```

### Step 6: Create App Service Plan and App Service

```powershell
$appServicePlanName = "asp-leave-management"
$appServiceName = "lm-app-$(Get-Random)"

# Create App Service Plan
az appservice plan create `
  --name $appServicePlanName `
  --resource-group $resourceGroupName `
  --sku B1 `
  --is-linux

# Create Web App
az webapp create `
  --resource-group $resourceGroupName `
  --plan $appServicePlanName `
  --name $appServiceName `
  --runtime "DOTNET:8.0"
```

### Step 7: Configure Managed Identity for App Service

```powershell
# Enable system-assigned managed identity
az webapp identity assign `
  --resource-group $resourceGroupName `
  --name $appServiceName

# Get the identity object ID
$identityObjectId = (az webapp identity show `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --query principalId -o tsv)

# Grant Key Vault access
az keyvault role assignment create `
  --vault-name $keyVaultName `
  --role "Key Vault Secrets Officer" `
  --assignee-object-id $identityObjectId `
  --assignee-principal-type ServicePrincipal
```

---

## Configuration

### Step 1: Register Azure AD Application

#### 1.1 Using Azure Portal
1. Go to **Azure Active Directory** → **App registrations**
2. Click **New registration**
3. Name: "Leave Management App"
4. Supported account types: "Accounts in this organizational directory only"
5. Click **Register**

#### 1.2 Configure Redirect URI
1. Go to **Authentication** tab
2. Add redirect URI: `https://your-app-service-name.azurewebsites.net/signin-oidc`
3. Click **Save**

#### 1.3 Create Client Secret
1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Copy the secret value and expiration date
4. ⚠️ **Save this securely!**

#### 1.4 Get Configuration Values
From **Overview** tab, copy:
- Client ID (Application ID)
- Tenant ID (Directory ID)

### Step 2: Add Secrets to Key Vault

```powershell
# Replace with your actual values
$tenantId = "YOUR_TENANT_ID"
$clientId = "YOUR_CLIENT_ID"
$clientSecret = "YOUR_CLIENT_SECRET"
$sqlConnectionString = "YOUR_SQL_CONNECTION_STRING"
$blobConnectionString = "YOUR_BLOB_CONNECTION_STRING"
$sendGridKey = "YOUR_SENDGRID_API_KEY"

# Add secrets to Key Vault
az keyvault secret set --vault-name $keyVaultName --name "AzureAd--TenantId" --value $tenantId
az keyvault secret set --vault-name $keyVaultName --name "AzureAd--ClientId" --value $clientId
az keyvault secret set --vault-name $keyVaultName --name "AzureAd--ClientSecret" --value $clientSecret
az keyvault secret set --vault-name $keyVaultName --name "ConnectionStrings--DefaultConnection" --value $sqlConnectionString
az keyvault secret set --vault-name $keyVaultName --name "AzureBlobStorage--ConnectionString" --value $blobConnectionString
az keyvault secret set --vault-name $keyVaultName --name "SendGrid--ApiKey" --value $sendGridKey
```

### Step 3: Configure App Settings

```powershell
# Set App Service configuration
az webapp config appsettings set `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --settings `
    ASPNETCORE_ENVIRONMENT="Production" `
    KeyVault__VaultUri="https://$keyVaultName.vault.azure.net/" `
    ApplicationInsights__ConnectionString="InstrumentationKey=$appInsightsKey;..."
```

---

## Deployment Steps

### Option 1: Using Azure CLI

```powershell
# Navigate to project
cd LeaveManagement

# Build for release
dotnet publish -c Release -o ./publish

# Deploy to App Service
az webapp deployment source config-zip `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --src (Compress-Archive -Path ./publish -CompressionLevel Optimal)
```

### Option 2: Using GitHub Actions (Recommended)

#### 2.1 Add Publish Profile to GitHub Secrets

```powershell
# Download publish profile
az webapp deployment list-publishing-profiles `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --query "[0].publishUrl" `
  --output tsv

# Get full profile (for GitHub Actions)
az webapp deployment list-publishing-profiles `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --xml
```

Copy the XML and add to GitHub:
1. Go to repo **Settings** → **Secrets and variables** → **Actions**
2. Add new secret: `AZURE_WEBAPP_PUBLISH_PROFILE` with the XML content
3. Add new secret: `AZURE_WEBAPP_NAME` with your app service name

#### 2.2 Push to Main Branch
```powershell
git add .
git commit -m "Add Azure configuration"
git push origin main
```

GitHub Actions will automatically build and deploy!

### Option 3: Using Visual Studio Publish

1. Right-click project → **Publish**
2. Select **Azure** → **Azure App Service**
3. Select your subscription and app service
4. Click **Publish**

---

## Database Migrations in Production

### Using Azure CLI with SSH

```powershell
# Enable SSH and restart app
az webapp remote-debugging set --resource-group $resourceGroupName --name $appServiceName --slot production --enable

# Connect via SSH (from Kudu console: https://<app-name>.scm.azurewebsites.net/webssh/host)
cd /home/site/wwwroot

# Run migrations
dotnet ef database update
```

Or run migrations before deployment:

```powershell
# In your CI/CD pipeline or local before publishing
cd LeaveManagement
dotnet ef database update --connection "YOUR_PROD_CONNECTION_STRING"
dotnet publish -c Release
```

---

## Monitoring & Maintenance

### View Application Insights

```powershell
# Get insights data
az monitor metrics list `
  --resource /subscriptions/{subscription-id}/resourceGroups/$resourceGroupName/providers/microsoft.insights/components/$appInsightsName `
  --metric-definitions
```

Or use the **Azure Portal** → **Application Insights** → Your app resource

### Enable Diagnostic Logs

```powershell
az webapp log config `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --application-logging filesystem `
  --level information
```

### View Logs

```powershell
# Stream logs in real-time
az webapp log tail --resource-group $resourceGroupName --name $appServiceName
```

---

## Environment Variables Reference

| Variable | Value | Environment |
|----------|-------|-------------|
| ASPNETCORE_ENVIRONMENT | Production | Production |
| KeyVault__VaultUri | https://[vault-name].vault.azure.net/ | Production |
| ConnectionStrings__DefaultConnection | (from Key Vault) | Production |
| AzureBlobStorage__ConnectionString | (from Key Vault) | Production |
| SendGrid__ApiKey | (from Key Vault) | Production |
| AzureAd__TenantId | (from Key Vault) | Production |
| AzureAd__ClientId | (from Key Vault) | Production |
| AzureAd__ClientSecret | (from Key Vault) | Production |

---

## Troubleshooting

### Issue: "Connection timeout to SQL Database"

**Solution:**
1. Check firewall rules allow your IP
2. Verify connection string format
3. Ensure database exists and is online

```powershell
# Test connection
sqlcmd -S <server>.database.windows.net -U <username> -P <password> -d <database>
```

### Issue: "Blob Storage upload fails"

**Solution:**
1. Verify connection string is correct
2. Ensure container name is 'attachments'
3. Check storage account access policies

```powershell
# Verify container exists
az storage container exists --account-name $storageAccountName --name attachments
```

### Issue: "Azure AD login shows error"

**Solution:**
1. Verify redirect URI matches exactly in App Registration
2. Check Client Secret hasn't expired
3. Ensure Key Vault access is configured

### Issue: "Application Insights shows no data"

**Solution:**
1. Verify connection string is set correctly
2. Check app is actually using the instrumentation key
3. Ensure Application Insights resource exists

---

## Post-Deployment Checklist

- [ ] Database migrations completed successfully
- [ ] Application loads without errors
- [ ] Azure AD login works
- [ ] Can create leave request with file upload
- [ ] Blob Storage contains uploaded files
- [ ] Manager dashboard shows pending requests
- [ ] Email notifications send successfully
- [ ] Application Insights shows telemetry data
- [ ] SSL/HTTPS is enabled
- [ ] Custom domain is configured (if applicable)

---

## Support

For issues or questions:
1. Check Application Insights logs
2. Review Azure Portal diagnostics
3. Check GitHub Actions workflow logs
4. Contact your Azure administrator

---

**Last Updated:** November 13, 2025
