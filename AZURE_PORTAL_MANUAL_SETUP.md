# Azure Portal Manual Setup Guide - Leave Management App
## (Without Key Vault - Using App Service Configuration Only)

**Last Updated**: November 15, 2025  
**Status**: Ready for Manual Azure Portal Configuration  
**Prerequisites**: Active Azure Subscription, SendGrid Account

---

## 📋 Overview

This guide provides **step-by-step Azure Portal instructions** to deploy the Leave Management application **WITHOUT Key Vault**. All secrets and configuration will be stored directly in App Service Application Settings.

**Key Difference from AZURE_SETUP_STEPS.md:**
- ❌ No Key Vault (commented out in code)
- ✅ Configuration stored in App Service Settings directly
- ✅ All manual Portal UI steps (no CLI commands)
- ✅ Simpler setup, fewer permissions required

---

## 🎯 Resource Names Reference

Use these consistent resource names throughout:

| Resource | Name | Details |
|----------|------|---------|
| Resource Group | `rg-leave-mgmt` | Contains all resources |
| SQL Server | `lm-sqlserver-{random}` | e.g., lm-sqlserver-7432 |
| SQL Database | `LeaveManagementDB` | Database for app |
| Storage Account | `lmstorage{random}` | e.g., lmstorage98765 |
| App Service Plan | `asp-leave-mgmt` | Basic B1 tier |
| App Service | `lm-app-{random}` | e.g., lm-app-5821 |
| Application Insights | `ai-leave-mgmt` | Monitoring & telemetry |

---

## ✅ Phase 1: Create Resource Group

### Step 1.1 - Create Resource Group

1. Go to **Azure Portal** → https://portal.azure.com
2. Search for **"Resource Groups"** in the top search bar
3. Click **+ Create**
4. Fill in:
   - **Subscription**: Select your subscription
   - **Resource group name**: `rg-leave-mgmt`
   - **Region**: Select closest to your location (e.g., `East US`, `West Europe`)
5. Click **Review + create** → **Create**
6. Wait for creation to complete (green checkmark)

✅ **Resource Group created**: `rg-leave-mgmt`

---

## ✅ Phase 2: Create Azure SQL Database

### Step 2.1 - Create SQL Server & Database

1. In Azure Portal, search for **"SQL databases"**
2. Click **+ Create**
3. On **Create SQL Database** form:
   - **Subscription**: Select your subscription
   - **Resource group**: Select `rg-leave-mgmt` (created above)
   - **Database name**: `LeaveManagementDB`
   - **Server**: Click **Create new**
     - **Server name**: `lm-sqlserver-####` (replace #### with a random 4-digit number, e.g., `lm-sqlserver-7432`)
     - **Server admin login**: `azureAdmin`
     - **Password**: Choose a **strong password** (min 8 chars, uppercase, number, special char)  
       **⚠️ Save this password securely — you'll need it later**
     - **Location**: Same as resource group (e.g., `East US`)
     - Click **OK**
   - **Compute + storage**: Click **Configure database** → Select **Basic (0 DTUs)** → Click **Apply**
4. Click **Review + create** → Review settings → Click **Create**
5. Wait for deployment (this may take 1-2 minutes)

✅ **SQL Server & Database created**

### Step 2.2 - Allow Azure Services to Access SQL Database

1. Go to **Resource Groups** → **rg-leave-mgmt**
2. Find and click on your **SQL Server** (name: `lm-sqlserver-####`)
3. In the left menu, click **Networking**
4. Under **Firewall rules**, find the rule **"Allow Azure services and resources to access this server"**
   - If not present, toggle **"Allow Azure services and resources..."** to **ON**
5. Click **Save**

✅ **Firewall configured to allow Azure services**

### Step 2.3 - Get SQL Connection String

1. Go to your **SQL Database** (`LeaveManagementDB`)
2. Click **Connection strings** (left menu)
3. Select **ADO.NET (SQL authentication)**
4. Copy the connection string
5. **Replace `{your_password}`** with the SQL password you created in Step 2.1
6. **Save this connection string** — you'll add it to App Service later

Example (after replacing password):
```
Server=tcp:lm-sqlserver-7432.database.windows.net,1433;Initial Catalog=LeaveManagementDB;Persist Security Info=False;User ID=azureAdmin;Password=YourPassword123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

✅ **Connection string saved**

---

## ✅ Phase 3: Create Azure Storage Account (Blob Storage)

### Step 3.1 - Create Storage Account

1. Search for **"Storage accounts"** in Azure Portal
2. Click **+ Create**
3. Fill in:
   - **Subscription**: Select your subscription
   - **Resource group**: `rg-leave-mgmt`
   - **Storage account name**: `lmstorage####` (replace with random 5-6 digit number, e.g., `lmstorage98765`)
     - ⚠️ Must be globally unique (lowercase letters & numbers only)
   - **Region**: Same as resource group
   - **Performance**: Standard
   - **Redundancy**: Locally-redundant storage (LRS)
4. Click **Review + create** → **Create**
5. Wait for deployment

✅ **Storage Account created**

### Step 3.2 - Create Blob Container

1. Go to your **Storage account** (`lmstorage####`)
2. In left menu, click **Containers** (under "Data storage")
3. Click **+ Container**
4. Fill in:
   - **Name**: `attachments`
   - **Public access level**: Private (No anonymous access)
5. Click **Create**

✅ **Blob container "attachments" created**

### Step 3.3 - Get Storage Connection String

1. Go to your **Storage account** (`lmstorage####`)
2. In left menu, click **Access keys** (under "Security + networking")
3. Under **key1**, click **Show keys**
4. Copy the **Connection string**
5. **Save this connection string** — you'll add it to App Service later

Example:
```
DefaultEndpointsProtocol=https;AccountName=lmstorage98765;AccountKey=...;EndpointSuffix=core.windows.net
```

✅ **Storage connection string saved**

---

## ✅ Phase 4: Create Application Insights

### Step 4.1 - Create Application Insights

1. Search for **"Application Insights"** in Azure Portal
2. Click **+ Create**
3. Fill in:
   - **Subscription**: Select your subscription
   - **Resource group**: `rg-leave-mgmt`
   - **Name**: `ai-leave-mgmt`
   - **Region**: Same as resource group
   - **Resource Mode**: Workspace-based
4. Click **Review + create** → **Create**
5. Wait for deployment

✅ **Application Insights created**

### Step 4.2 - Get Application Insights Connection String

1. Go to **Application Insights** (`ai-leave-mgmt`)
2. Click **Overview** in left menu
3. Look for **Connection String** at the top right
4. Click **Copy** button
5. **Save this connection string** — you'll add it to App Service later

Example:
```
InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint=https://eastus-1.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/
```

✅ **Application Insights connection string saved**

---

## ✅ Phase 5: Create App Service Plan & App Service

### Step 5.1 - Create App Service Plan

1. Search for **"App Service plans"** in Azure Portal
2. Click **+ Create**
3. Fill in:
   - **Subscription**: Select your subscription
   - **Resource group**: `rg-leave-mgmt`
   - **Name**: `asp-leave-mgmt`
   - **Operating System**: Linux
   - **Region**: Same as resource group
   - **Pricing plan**: Click **Change size** → Select **Basic B1** → Click **Apply** (~$10/month)
4. Click **Review + create** → **Create**
5. Wait for deployment

✅ **App Service Plan created**

### Step 5.2 - Create App Service (Web App)

1. Search for **"App Services"** in Azure Portal
2. Click **+ Create**
3. Fill in:
   - **Subscription**: Select your subscription
   - **Resource group**: `rg-leave-mgmt`
   - **Name**: `lm-app-####` (e.g., `lm-app-5821`) — must be globally unique
   - **Publish**: Code
   - **Runtime stack**: .NET 8 (LTS)
   - **Operating System**: Linux
   - **Region**: Same as resource group
   - **App Service Plan**: Select `asp-leave-mgmt` (created above)
4. Click **Review + create** → **Create**
5. Wait for deployment

✅ **App Service created**

### Step 5.3 - Enable Managed Identity (Optional but Recommended)

1. Go to your **App Service** (`lm-app-####`)
2. In left menu, click **Identity** (under "Settings")
3. Under **System assigned**, toggle **Status** to **ON**
4. Click **Save**

✅ **Managed Identity enabled** (useful for future Key Vault access)

---

## ✅ Phase 6: Configure App Service Settings

### Step 6.1 - Add Application Settings

1. Go to your **App Service** (`lm-app-####`)
2. In left menu, click **Configuration** (under "Settings")
3. Click **+ New application setting** for each setting below:

#### 6.1.1 - Environment Setting

| Name | Value |
|------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |

#### 6.1.2 - Database Configuration

| Name | Value |
|------|-------|
| `ConnectionStrings__DefaultConnection` | *Paste your SQL connection string from Step 2.3* |

Example:
```
Server=tcp:lm-sqlserver-7432.database.windows.net,1433;Initial Catalog=LeaveManagementDB;Persist Security Info=False;User ID=azureAdmin;Password=YourPassword123!;...
```

#### 6.1.3 - Blob Storage Configuration

| Name | Value |
|------|-------|
| `ConnectionStrings__AzureBlobStorage` | *Paste your Storage connection string from Step 3.3* |
| `AzureBlobStorage__ContainerName` | `attachments` |

#### 6.1.4 - SendGrid Email Configuration

First, get your **SendGrid API Key**:
- Go to https://app.sendgrid.com
- Log in to your SendGrid account
- Go to **Settings** → **API Keys**
- Create a new API key (or copy existing)
- **Copy and save this key**

Then add these settings:

| Name | Value |
|------|-------|
| `SendGrid__ApiKey` | *Your SendGrid API key* |
| `SendGrid__FromEmail` | `noreply@yourdomain.com` (or any sender email) |
| `SendGrid__FromName` | `Leave Management System` |

#### 6.1.5 - Application Insights Configuration

| Name | Value |
|------|-------|
| `ApplicationInsights__ConnectionString` | *Paste your App Insights connection string from Step 4.2* |

### Step 6.2 - Save All Settings

1. After adding all settings, click **Save** at the top
2. Click **Continue** when prompted to confirm

✅ **All application settings configured**

### Step 6.3 - Enable HTTPS Only

1. Still in **Configuration** page
2. Click **General settings** tab
3. Under **HTTPS Only**, toggle to **ON**
4. Click **Save**

✅ **HTTPS enforced**

---

## ✅ Phase 7: Deploy Application Code

### Option A: Deploy from GitHub (Recommended for Continuous Deployment)

#### Step 7A.1 - Prepare GitHub

1. Ensure your code is in a GitHub repository with:
   - Branch: `main` (or `master`)
   - Workflow file: `.github/workflows/deploy.yml` (should already exist)

#### Step 7A.2 - Configure Deployment

1. Go to your **App Service** (`lm-app-####`)
2. In left menu, click **Deployment center** (under "Deployment")
3. Select **GitHub** as source
4. Click **Authorize** and log in to your GitHub account
5. Fill in:
   - **Organization**: Your GitHub username
   - **Repository**: `Leavemanagement353` (or your repo name)
   - **Branch**: `main`
6. Click **Save**

✅ **GitHub deployment configured**

The app will automatically build and deploy when you push to the `main` branch.

### Option B: Deploy Using Publish Profile (One-time Deployment)

#### Step 7B.1 - Get Publish Profile

1. Go to your **App Service** (`lm-app-####`)
2. Click **Download publish profile** button (top right)
3. A file `lm-app-####.PublishSettings` will download
4. **Keep this file safe** — contains deployment credentials

#### Step 7B.2 - Deploy Using Visual Studio

1. Open your LeaveManagement project in Visual Studio
2. Right-click **LeaveManagement** project → **Publish**
3. Click **Import profile** → Select the `.PublishSettings` file
4. Click **Finish**
5. Click **Publish**

✅ **Application deployed**

---

## ✅ Phase 8: Run Database Migrations

After the app is deployed, you need to run migrations to create the database schema.

### Step 8.1 - Use Kudu Console to Run Migrations

1. Go to your **App Service** (`lm-app-####`)
2. In left menu, click **Advanced Tools** (under "Development tools")
3. Click **Go** to open Kudu
4. Click **Debug console** → **PowerShell**
5. Navigate to: `d:\home\site\wwwroot`
6. Run:
```powershell
dotnet ef database update
```

✅ **Database migrations completed**

Alternatively, you can run migrations locally before deployment:

1. On your local machine:
```powershell
cd LeaveManagement
dotnet ef database update --connection "Your SQL Connection String"
```

---

## ✅ Phase 9: Test the Deployment

### Step 9.1 - Access the Application

1. Go to your **App Service** (`lm-app-####`)
2. Click **Overview**
3. Copy the **URL** (e.g., `https://lm-app-5821.azurewebsites.net`)
4. Open in a browser

✅ **App is running**

### Step 9.2 - Test Key Functionality

#### Test 1: Login
- Click **Login** button
- You should see the login form (local development mock auth)
- Default credentials: `user` / `password`

#### Test 2: Create Leave Request
1. Log in
2. Go to **Leaves** → **Create**
3. Fill in:
   - Start Date: Select a date using the calendar picker
   - End Date: Select an end date
   - Type: Select a leave type
   - Reason: (optional) Enter a reason
4. Click **Create**
5. Verify the request appears in the Leaves list

#### Test 3: View Leave Details
1. Click on a leave request
2. Verify details display correctly
3. Verify **Reason displays without extra dashes** (our fix)

#### Test 4: Upload Attachment (Test Blob Storage)
1. Create another leave request
2. Add a file attachment (PDF, DOC, JPG)
3. Click **Create**
4. Go back to the request details
5. Verify **Download attachment** link works

✅ **Core functionality working**

### Step 9.3 - Check Application Insights

1. Go to **Application Insights** (`ai-leave-mgmt`)
2. Click **Live Metrics** (left menu)
3. Perform actions in the app (login, create leave, etc.)
4. You should see requests appearing in real-time

✅ **Application Insights monitoring working**

---

## 📊 Configuration Summary

Here's what you've configured:

| Component | Service | Setting Name | Value |
|-----------|---------|--------------|-------|
| **Web App** | App Service | `ASPNETCORE_ENVIRONMENT` | Production |
| **Database** | SQL Server | `ConnectionStrings__DefaultConnection` | SQL conn string |
| **File Storage** | Blob | `ConnectionStrings__AzureBlobStorage` | Storage conn string |
| **File Storage** | Blob | `AzureBlobStorage__ContainerName` | attachments |
| **Email** | SendGrid | `SendGrid__ApiKey` | Your API key |
| **Email** | SendGrid | `SendGrid__FromEmail` | noreply@yourdomain.com |
| **Email** | SendGrid | `SendGrid__FromName` | Leave Management System |
| **Monitoring** | App Insights | `ApplicationInsights__ConnectionString` | App Insights conn string |

---

## 🔒 Security Best Practices

1. **SQL Password**: Strong password (8+ chars, mixed case, numbers, symbols)
2. **SendGrid API Key**: Never commit to source code; use App Settings only
3. **HTTPS**: Always enabled (toggled in Step 6.3)
4. **Blob Storage**: Set to Private (no anonymous access)
5. **Managed Identity**: Enabled for future Key Vault access (Step 5.3)

---

## 🐛 Troubleshooting

### Issue: "500 Internal Server Error" when accessing app

**Solution**:
1. Go to **App Service** → **Log stream** (left menu)
2. Look for error messages
3. Most common causes:
   - SQL connection string incorrect → Check Step 2.3
   - Storage connection string incorrect → Check Step 3.3
   - SendGrid API key invalid → Check SendGrid account
   - Environment variable typo → Check Step 6.1 setting names exactly

### Issue: "Cannot upload file to Blob Storage"

**Solution**:
1. Verify `ConnectionStrings__AzureBlobStorage` is set correctly (Step 6.1.3)
2. Verify blob container exists: Go to Storage account → Containers → Should see `attachments`
3. Check App Service logs for detailed error

### Issue: "Database migration failed"

**Solution**:
1. Verify SQL connection string is correct and password is updated (Step 2.3)
2. Run migrations manually on local machine:
```powershell
cd LeaveManagement
dotnet ef database update --connection "Your SQL Conn String"
```
3. If migrations fail, check the SQL database exists (Step 2.1)

### Issue: "Email notifications not sending"

**Solution**:
1. Verify SendGrid API key is correct → Step 6.1.4
2. Verify `SendGrid__FromEmail` is a valid SendGrid sender email
3. Check SendGrid activity log: https://app.sendgrid.com/email_activity

---

## 📞 Helpful Links

- **Azure Portal**: https://portal.azure.com
- **SendGrid Dashboard**: https://app.sendgrid.com
- **Azure Documentation**: https://learn.microsoft.com/en-us/azure/
- **App Service Troubleshooting**: https://learn.microsoft.com/en-us/azure/app-service/troubleshoot-common-app-service-errors
- **SQL Database Documentation**: https://learn.microsoft.com/en-us/azure/azure-sql/database/

---

## ✨ Next Steps (After Deployment)

1. **Add Azure AD Login** (if you have organizational Azure AD):
   - Requires Azure AD app registration
   - Can be added later without re-deploying the app
   - Update `appsettings.Production.json` with Azure AD credentials

2. **Enable Key Vault** (when you have permissions):
   - Uncomment Key Vault code in `Program.cs`
   - Move secrets from App Settings to Key Vault
   - Grant Managed Identity access to Key Vault

3. **Setup Custom Domain** (optional):
   - Purchase domain
   - Configure DNS records to point to App Service
   - Add SSL certificate

4. **Setup Monitoring Alerts**:
   - Go to Application Insights → Alerts
   - Create alerts for high error rates, down time, etc.

5. **Configure Backups**:
   - Go to SQL Database → Backups
   - Setup automated backup retention

---

## 📝 Saved Configuration Values Checklist

Before finishing, create a file or document and save these values securely:

```
✅ SQL Server Name: lm-sqlserver-____
✅ SQL Database: LeaveManagementDB
✅ SQL Admin Username: azureAdmin
✅ SQL Password: _________________ (SECURE!)
✅ SQL Connection String: Server=tcp:...

✅ Storage Account: lmstorage_____
✅ Blob Container: attachments
✅ Storage Connection String: DefaultEndpointsProtocol=https;...

✅ App Service Name: lm-app-____
✅ App Service URL: https://lm-app-____.azurewebsites.net

✅ Application Insights Name: ai-leave-mgmt
✅ App Insights Connection String: InstrumentationKey=...

✅ SendGrid API Key: _________________ (SECURE!)
✅ SendGrid From Email: noreply@yourdomain.com

✅ Resource Group: rg-leave-mgmt
✅ Subscription ID: ________________
```

---

**Status**: ✅ Ready for Production  
**Key Vault**: ❌ Not configured (commented out in code)  
**Database**: SQL Azure  
**Storage**: Azure Blob  
**Authentication**: Local (Development) / Azure AD (Production-ready)  
**Monitoring**: Application Insights enabled  

Last Updated: November 15, 2025
