# Azure AD Logout URL Configuration

## Where to Add the Logout Callback URL

The URL `https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signout-callback-oidc` needs to be added in **Azure AD App Registration**.

## Step-by-Step Instructions

### 1. Go to Azure Portal
- Navigate to: https://portal.azure.com
- Sign in with your Azure account

### 2. Open Azure Active Directory
- Click on **Azure Active Directory** (or search for it)
- In the left menu, click **App registrations**

### 3. Select Your Application
- Find and click on your app registration (e.g., "Leave Management App")

### 4. Add the Logout Redirect URI

#### Option A: Add as Redirect URI (Recommended)
1. Click on **Authentication** in the left menu
2. Under **Redirect URIs**, click **+ Add URI**
3. Select **Web** as the platform type
4. Enter: `https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signout-callback-oidc`
5. Click **Save**

#### Option B: Add as Front-channel Logout URL (If Available)
1. Click on **Authentication** in the left menu
2. Scroll down to **Front-channel logout URL** section
3. Enter: `https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signout-callback-oidc`
4. Click **Save**

### 5. Verify All Redirect URIs

Make sure you have **both** of these redirect URIs configured:

✅ **Sign-in callback:**
```
https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signin-oidc
```

✅ **Sign-out callback:**
```
https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signout-callback-oidc
```

## Complete Redirect URI List

Your Azure AD App Registration should have these redirect URIs:

| Type | URL |
|------|-----|
| Sign-in | `https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signin-oidc` |
| Sign-out | `https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signout-callback-oidc` |

## How It Works

1. **User clicks Logout** → Application calls Azure AD logout endpoint
2. **Azure AD signs out user** → Clears Azure AD session
3. **Azure AD redirects back** → To `/signout-callback-oidc`
4. **Application handles callback** → Redirects to `/Account/Login?logout=true`
5. **Login page shows** → Without auto-login (because of `logout=true` parameter)

## Verification

After adding the URL:

1. **Test Logout:**
   - Log in to your application
   - Click the "Logout" button
   - You should be redirected to Azure AD logout page
   - Then redirected back to your login page
   - Login page should NOT auto-login you

2. **Check Application Logs:**
   - If logout doesn't work, check Application Insights logs
   - Look for errors related to "signout-callback-oidc"

## Troubleshooting

### Issue: "Redirect URI mismatch" error
- **Solution:** Make sure the URL is exactly: `https://lm-app-353-hgg7ghfreubtagha.canadacentral-01.azurewebsites.net/signout-callback-oidc`
- No trailing slash
- Exact match required

### Issue: Logout redirects but auto-logs in again
- **Solution:** Check that `Program.cs` has the `OnSignedOutCallbackRedirect` event handler configured
- Verify the login page checks for `logout=true` query parameter

### Issue: 404 on signout-callback-oidc
- **Solution:** Ensure `SignedOutCallbackPath` is set in `Program.cs` (already configured)
- Verify the route is mapped correctly

---

**Important:** After adding the redirect URI, it may take a few minutes for the changes to propagate. Wait 2-3 minutes before testing