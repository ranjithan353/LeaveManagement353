# Azure Implementation Steps - Quick Reference

## 🎯 Executive Summary

The Leave Management application is **fully implemented and ready for Azure deployment**. This document provides the specific manual steps needed to deploy to Azure.

### Current Status
✅ **All code is complete and tested**
- 8 unit/integration tests passing
- Project builds successfully
- Local development works with mock auth
- All Azure integrations coded (Azure AD, Blob Storage, SQL, Key Vault, Application Insights, SendGrid)

---

## 📋 Prerequisites Before Starting

Ensure you have:
1. **Azure Subscription** - Active subscription with permissions to create resources
2. **Azure CLI** - Installed on your machine (`az --version`)
3. **SendGrid Account** - For email notifications (free tier available)
4. **GitHub Account** - For storing code and enabling GitHub Actions CI/CD

### Install Azure CLI (if not already installed)
```powershell
# Download and install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-windows

# Verify installation
az --version

# Login to Azure
az login
```

---

## 🚀 Step-by-Step Azure Deployment

### Phase 1: Azure Resource Creation (15-20 minutes)

#### 1.1 Create Resource Group
```powershell
$resourceGroupName = "rg-leave-mgmt"
$location = "eastus"  # Change to your preferred region

az group create `
  --name $resourceGroupName `
  --location $location

echo "✅ Resource Group created: $resourceGroupName"
```

#### 1.2 Create Azure SQL Database
```powershell
# Set variables
$sqlServerName = "lm-sqlserver-$(Get-Random -Minimum 1000 -Maximum 9999)"
$databaseName = "LeaveManagementDB"
$adminLogin = "azureAdmin"
$adminPassword = "P@ssw0rd123$YourSecurePass"  # ⚠️ CHANGE THIS!

# Create SQL Server
az sql server create `
  --resource-group $resourceGroupName `
  --name $sqlServerName `
  --location $location `
  --admin-user $adminLogin `
  --admin-password $adminPassword

# Create Database
az sql db create `
  --resource-group $resourceGroupName `
  --server $sqlServerName `
  --name $databaseName `
  --edition Basic `
  --compute-model Serverless

# Configure Firewall - Allow Azure Services
az sql server firewall-rule create `
  --resource-group $resourceGroupName `
  --server $sqlServerName `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0

# Get connection string
$connString = az sql db show-connection-string `
  --admin-user $adminLogin `
  --server $sqlServerName `
  --name $databaseName `
  --client sqlserver -o tsv

# Replace password placeholder
$connString = $connString.Replace("{your_password}", $adminPassword)

echo "✅ SQL Database created: $databaseName"
echo "📝 Connection String: $connString"
echo "⚠️  SAVE THIS CONNECTION STRING - You'll need it later!"
```

#### 1.3 Create Azure Storage Account (Blob Storage)
```powershell
$storageAccountName = "lmstorage$(Get-Random -Minimum 10000 -Maximum 99999)"

# Create storage account
az storage account create `
  --resource-group $resourceGroupName `
  --name $storageAccountName `
  --location $location `
  --sku Standard_LRS `
  --kind StorageV2 `
  --access-tier Hot

# Create blob container
az storage container create `
  --account-name $storageAccountName `
  --name attachments `
  --public-access off

# Get connection string
$blobConnString = az storage account show-connection-string `
  --resource-group $resourceGroupName `
  --name $storageAccountName `
  --query connectionString -o tsv

echo "✅ Blob Storage created: $storageAccountName"
echo "📝 Connection String: $blobConnString"
echo "⚠️  SAVE THIS CONNECTION STRING!"
```

#### 1.4 Create Azure Key Vault
```powershell
$keyVaultName = "kv-lm-$(Get-Random -Minimum 10000 -Maximum 99999)"

# Create Key Vault
az keyvault create `
  --resource-group $resourceGroupName `
  --name $keyVaultName `
  --location $location `
  --enable-rbac-authorization

echo "✅ Key Vault created: $keyVaultName"
echo "📝 Vault URI: https://$keyVaultName.vault.azure.net/"
```

#### 1.5 Create Application Insights
```powershell
$appInsightsName = "ai-leave-mgmt"

# Create Application Insights
az monitor app-insights component create `
  --app $appInsightsName `
  --location $location `
  --resource-group $resourceGroupName `
  --application-type web

# Get connection string
$appInsightsConn = az monitor app-insights component show `
  --app $appInsightsName `
  --resource-group $resourceGroupName `
  --query connectionString -o tsv

echo "✅ Application Insights created"
echo "📝 Connection String: $appInsightsConn"
```

#### 1.6 Create App Service Plan & App Service
```powershell
$appServicePlanName = "asp-leave-mgmt"
$appServiceName = "lm-app-$(Get-Random -Minimum 10000 -Maximum 99999)"

# Create App Service Plan (Basic B1 = ~$10/month)
az appservice plan create `
  --name $appServicePlanName `
  --resource-group $resourceGroupName `
  --sku B1 `
  --is-linux

# Create Web App (.NET 8)
az webapp create `
  --resource-group $resourceGroupName `
  --plan $appServicePlanName `
  --name $appServiceName `
  --runtime "DOTNET:8.0"

# Enable system-assigned managed identity
az webapp identity assign `
  --resource-group $resourceGroupName `
  --name $appServiceName

echo "✅ App Service created: $appServiceName"
echo "📝 App URL: https://$appServiceName.azurewebsites.net"

# Get managed identity object ID (needed for Key Vault access)
$identityObjectId = az webapp identity show `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --query principalId -o tsv

echo "📝 Managed Identity ID: $identityObjectId"
```

---

### Phase 2: Azure AD Application Registration (10 minutes)

#### 2.1 Register Application in Azure AD

**Using Azure Portal:**
1. Go to **Azure Active Directory** → **App registrations**
2. Click **+ New registration**
3. Fill in:
   - **Name**: "Leave Management App"
   - **Supported account types**: "Accounts in this organizational directory only"
   - **Redirect URI**: `https://<your-app-service-name>.azurewebsites.net/signin-oidc`
4. Click **Register**

#### 2.2 Get Azure AD Configuration
From the **Overview** tab, copy:
- **Application (Client) ID** → Save as `{CLIENT_ID}`
- **Directory (Tenant) ID** → Save as `{TENANT_ID}`

#### 2.3 Create Client Secret
1. Go to **Certificates & secrets**
2. Click **+ New client secret**
3. Copy the **Value** (not the ID) → Save as `{CLIENT_SECRET}`
4. ⚠️ **Note the expiration date - you'll need to renew before it expires**

#### 2.4 Grant API Permissions
1. Go to **API permissions**
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Select **Delegated permissions**
5. Add: `openid`, `profile`, `email`
6. Click **Grant admin consent** (if available)

---

### Phase 3: Store Secrets in Key Vault (5 minutes)

```powershell
# Variables from previous steps
$tenantId = "{TENANT_ID}"           # From Azure AD
$clientId = "{CLIENT_ID}"            # From Azure AD
$clientSecret = "{CLIENT_SECRET}"    # From Azure AD
$sqlConnString = "{YOUR_SQL_CONN_STRING}"      # From Step 1.2
$blobConnString = "{YOUR_BLOB_CONN_STRING}"    # From Step 1.3
$appInsightsConn = "{YOUR_INSIGHTS_CONN}"      # From Step 1.5
$sendGridKey = "{YOUR_SENDGRID_KEY}"           # From SendGrid

# Grant Managed Identity access to Key Vault
az keyvault role assignment create `
  --vault-name $keyVaultName `
  --role "Key Vault Secrets Officer" `
  --assignee-object-id $identityObjectId `
  --assignee-principal-type ServicePrincipal

# Add secrets to Key Vault
az keyvault secret set `
  --vault-name $keyVaultName `
  --name "AzureAd--TenantId" `
  --value $tenantId

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "AzureAd--ClientId" `
  --value $clientId

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "AzureAd--ClientSecret" `
  --value $clientSecret

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "ConnectionStrings--DefaultConnection" `
  --value $sqlConnString

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "AzureBlobStorage--ConnectionString" `
  --value $blobConnString

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "ApplicationInsights--ConnectionString" `
  --value $appInsightsConn

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "SendGrid--ApiKey" `
  --value $sendGridKey

echo "✅ All secrets stored in Key Vault"
```

---

### Phase 4: Configure App Service Settings (5 minutes)

```powershell
# Set environment to Production
az webapp config appsettings set `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --settings `
    ASPNETCORE_ENVIRONMENT="Production" `
    KeyVault__VaultUri="https://$keyVaultName.vault.azure.net/"

# Enable HTTPS only
az webapp update `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --https-only true

echo "✅ App Service configured"
```

---

### Phase 5: Deploy Application (10 minutes)

#### Option A: Using GitHub Actions (Recommended - Automated Deployments)

**5A.1 Get Publish Profile**
```powershell
# Download publish profile
$publishProfile = az webapp deployment list-publishing-profiles `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --xml

# Save to file
$publishProfile | Out-File -FilePath "publish-profile.xml" -Encoding UTF8

echo "✅ Publish profile saved to: publish-profile.xml"
echo "📝 Copy the entire content for GitHub Secrets"
```

**5A.2 Add GitHub Secrets**
1. Go to your GitHub repository
2. **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add `AZURE_WEBAPP_PUBLISH_PROFILE`:
   - Paste the entire XML content from publish-profile.xml
5. Click **New repository secret**
6. Add `AZURE_WEBAPP_NAME`:
   - Value: `{your-app-service-name}` (from Step 1.6)

**5A.3 Push to Main Branch**
```powershell
cd LeaveManagement
git add .
git commit -m "Azure deployment ready"
git push origin main
```

The GitHub Actions workflow (`.github/workflows/deploy.yml`) will:
1. Build the project
2. Run all tests
3. Deploy to Azure App Service

Monitor the deployment at: `Repository` → **Actions** tab

#### Option B: Manual Deployment with Azure CLI

```powershell
# Navigate to project
cd LeaveManagement

# Publish for release
dotnet publish -c Release -o ./publish

# Deploy to App Service
cd publish
$zipPath = "app.zip"
Compress-Archive -Path * -DestinationPath $zipPath

az webapp deployment source config-zip `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --src-path $zipPath

echo "✅ Application deployed to Azure"
echo "🌐 Access at: https://$appServiceName.azurewebsites.net"
```

---

### Phase 6: Run Database Migrations (5 minutes)

```powershell
# Get the SQL connection string with your password
$connString = "Server=tcp:$sqlServerName.database.windows.net,1433;Initial Catalog=$databaseName;Persist Security Info=False;User ID=$adminLogin;Password=$adminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Run migrations
cd LeaveManagement
dotnet ef database update --connection $connString

echo "✅ Database migrations completed"
```

---

## ✅ Verification Checklist

After deployment, verify everything works:

### 1. Check App Service Health
```powershell
az webapp show `
  --resource-group $resourceGroupName `
  --name $appServiceName `
  --query "state"
```

### 2. Test Application Access
Open browser: `https://{app-service-name}.azurewebsites.net`

Expected: Leave Management login page appears

### 3. Test Azure AD Login
1. Click "Login"
2. You should be redirected to Microsoft login
3. Sign in with your organizational account
4. Should return to app

### 4. Test Database Connection
- Create a leave request (should save to Azure SQL)
- Navigate to "Leaves" page
- Request should appear

### 5. Test Blob Storage Upload
- Create a leave request with file attachment
- File should upload to Azure Blob Storage
- Should be able to download from details page

### 6. Check Application Insights
```powershell
az monitor metrics list `
  --resource /subscriptions/{subscription-id}/resourceGroups/$resourceGroupName/providers/microsoft.insights/components/$appInsightsName `
  --metric "RequestCount"
```

Or in Azure Portal: Resource Group → Application Insights → View Logs

---

## 📊 Resource Costs Estimate

| Service | SKU | Est. Monthly Cost |
|---------|-----|------------------|
| App Service | B1 | $10 |
| SQL Database | Basic | $5 |
| Blob Storage | 1GB | <$1 |
| Key Vault | 1 op/sec | $0.34 |
| Application Insights | 100MB/month | $1 |
| **Total** | | **~$16/month** |

*Costs vary by region and usage. Always check Azure Pricing Calculator.*

---

## 🐛 Troubleshooting

### Issue: "Deployment Failed"
```powershell
# Check deployment logs
az webapp log tail --resource-group $resourceGroupName --name $appServiceName
```

### Issue: "SQL Connection Timeout"
```powershell
# Verify firewall rule
az sql server firewall-rule list --resource-group $resourceGroupName --server $sqlServerName

# Your IP might not be whitelisted - add it:
az sql server firewall-rule create `
  --resource-group $resourceGroupName `
  --server $sqlServerName `
  --name "YourDevMachine" `
  --start-ip-address {YOUR_PUBLIC_IP} `
  --end-ip-address {YOUR_PUBLIC_IP}
```

### Issue: "Azure AD Login Not Working"
- Verify redirect URI in App Registration matches exactly: `https://{app-name}.azurewebsites.net/signin-oidc`
- Ensure Client Secret hasn't expired
- Check Key Vault has correct Azure AD secrets

### Issue: "Blob Upload Failed"
```powershell
# Verify container exists
az storage container exists --account-name $storageAccountName --name attachments

# Check connection string
az storage account show-connection-string --name $storageAccountName
```

---

## 📞 Support Resources

- **Azure Documentation**: https://docs.microsoft.com/azure/
- **Azure CLI Reference**: https://docs.microsoft.com/cli/azure/
- **.NET 8 Docs**: https://learn.microsoft.com/en-us/dotnet/
- **Razor Pages**: https://learn.microsoft.com/en-us/aspnet/core/razor-pages/
- **Azure AD B2C**: https://learn.microsoft.com/en-us/azure/active-directory-b2c/

---

## 🎓 Next Steps After Deployment

1. **Enable Custom Domain** (Optional)
   - Purchase domain
   - Configure DNS
   - Add SSL certificate

2. **Setup Monitoring Alerts**
   - High error rate
   - App Service down
   - Slow response times

3. **Configure Backups**
   - SQL Database backups
   - Application Insights data export

4. **Plan Updates & Maintenance**
   - Weekly deployments
   - Dependency updates
   - Security patches

---

## 📝 Quick Reference - All Saved Values

Keep these values safe:

```
Resource Group: {resourceGroupName}
Region: {location}

SQL Server: {sqlServerName}
SQL Database: {databaseName}
SQL Admin: {adminLogin}
SQL Password: {adminPassword}
SQL Connection String: {connString}

Storage Account: {storageAccountName}
Blob Connection String: {blobConnString}

Key Vault: {keyVaultName}
Key Vault URI: https://{keyVaultName}.vault.azure.net/

App Service Name: {appServiceName}
App Service URL: https://{appServiceName}.azurewebsites.net

Azure AD Tenant ID: {tenantId}
Azure AD Client ID: {clientId}
Azure AD Client Secret: {clientSecret} ⚠️ KEEP SECRET!

Application Insights: {appInsightsName}
Application Insights Connection: {appInsightsConn}

SendGrid API Key: {sendGridKey} ⚠️ KEEP SECRET!
```

---

**Last Updated**: November 13, 2025  
**Status**: Ready for Production Deployment ✅

---

## 🚦 Phase X: Azure AD Setup & Integration (No Key Vault)

### 1. Register Application in Azure AD
1. Go to **Azure Portal** → **Azure Active Directory** → **App registrations**
2. Click **+ New registration**
3. Fill in:
   - **Name**: Leave Management App
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: `https://<your-app-service-name>.azurewebsites.net/signin-oidc`
4. Click **Register**

### 2. Get Azure AD Credentials
1. In the new app registration, go to **Overview**
2. Copy these values:
   - **Application (client) ID** → `{CLIENT_ID}`
   - **Directory (tenant) ID** → `{TENANT_ID}`

### 3. Create Client Secret
1. Go to **Certificates & secrets**
2. Click **+ New client secret**
3. Add a description, set expiration, click **Add**
4. Copy the **Value** (not the ID) → `{CLIENT_SECRET}`
   - ⚠️ Save this securely! You can't view it again after leaving the page.

### 4. Grant API Permissions
1. Go to **API permissions**
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Select **Delegated permissions**
5. Add: `openid`, `profile`, `email`
6. Click **Grant admin consent** (if available)

### 5. Update App Service Configuration (No Key Vault)
Go to your **App Service** → **Configuration** → **Application settings** and add:

| Name | Value |
|------|-------|
| `AzureAd__TenantId` | `{TENANT_ID}` |
| `AzureAd__ClientId` | `{CLIENT_ID}` |
| `AzureAd__ClientSecret` | `{CLIENT_SECRET}` |

Click **Save** to apply changes.

### 6. Test Azure AD Login
1. Open your app URL: `https://<your-app-service-name>.azurewebsites.net`
2. Click **Login**
3. You should be redirected to Microsoft login
4. Sign in with your organizational account
5. You should return to the app, authenticated

---

**Note:**
- No Key Vault is required for this setup. All secrets are stored in App Service settings.
- If you enable Key Vault later, move these secrets there and uncomment the Key Vault code in `Program.cs`.
- For security, rotate the client secret before expiration and update App Service settings.

---
