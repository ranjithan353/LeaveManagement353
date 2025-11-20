using System.Linq;
using System.Security.Claims;
using LeaveManagement.Data;
using LeaveManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
configuration.AddEnvironmentVariables();

// Add services
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // required for MicrosoftIdentity UI controllers

// Database
if (builder.Environment.IsProduction())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Data/leave.db"));
}

// Authentication
if (builder.Environment.IsProduction())
{
    builder.Services.AddMicrosoftIdentityWebAppAuthentication(configuration, "AzureAd");
    
    // Configure OpenIdConnect options after adding authentication
    builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(
        Microsoft.Identity.Web.Constants.AzureAd, options =>
        {
            // Set signed out callback path
            options.SignedOutCallbackPath = "/signout-callback-oidc";
            
            // Ensure we get claims from userinfo endpoint
            options.GetClaimsFromUserInfoEndpoint = true;
            
            // Save tokens for debugging
            options.SaveTokens = true;
            
            // Request specific scopes to ensure we get oid claim
            if (!options.Scope.Contains("openid"))
                options.Scope.Add("openid");
            if (!options.Scope.Contains("profile"))
                options.Scope.Add("profile");
            
            // Store the existing events if any
            var existingEvents = options.Events;
            
            options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    // Call existing handler if present
                    existingEvents?.OnRedirectToIdentityProvider?.Invoke(context);
                    
                    // Force account selection on login (unless coming from logout)
                    if (!context.Properties.Items.ContainsKey("prompt"))
                    {
                        context.ProtocolMessage.Prompt = "select_account";
                    }
                    return System.Threading.Tasks.Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    // Call existing handler if present
                    if (existingEvents?.OnTokenValidated != null)
                    {
                        await existingEvents.OnTokenValidated(context);
                    }
                    
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                    
                    // Check if Principal exists
                    if (context.Principal == null)
                    {
                        logger?.LogError("OnTokenValidated: Principal is NULL!");
                        return;
                    }
                    
                    // Log all claims received for debugging
                    var claims = context.Principal.Claims?.ToList() ?? new List<Claim>();
                    logger?.LogInformation("Token validated. Received {Count} claims", claims.Count);
                    
                    if (claims.Count == 0)
                    {
                        logger?.LogError("CRITICAL: No claims found in authentication token!");
                        logger?.LogError("Principal Identity Type: {Type}, IsAuthenticated: {IsAuth}", 
                            context.Principal.Identity?.GetType().Name, context.Principal.Identity?.IsAuthenticated);
                        
                        // Try to get claims from the token response
                        if (context.TokenEndpointResponse != null)
                        {
                            logger?.LogInformation("Token endpoint response available");
                        }
                    }
                    else
                    {
                        foreach (var claim in claims)
                        {
                            logger?.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                        }
                    }
                    
                    // Check for oid claim specifically
                    var oidClaim = claims.FirstOrDefault(c => c.Type == "oid" || c.Type == ClaimTypes.NameIdentifier);
                    if (oidClaim != null)
                    {
                        logger?.LogInformation("Found UserId claim: {Type} = {Value}", oidClaim.Type, oidClaim.Value);
                    }
                    else
                    {
                        logger?.LogWarning("No oid or NameIdentifier claim found in token!");
                        
                        // Try to extract from token response if available
                        if (context.TokenEndpointResponse != null)
                        {
                            logger?.LogInformation("Token endpoint response available");
                            // Log token response keys for debugging
                            var responseKeys = context.TokenEndpointResponse.Parameters?.Keys?.ToList() ?? new List<string>();
                            logger?.LogInformation("Token response parameters: {Keys}", string.Join(", ", responseKeys));
                        }
                    }
                    
                    // Ensure oid is mapped to NameIdentifier if not already
                    var identity = context.Principal.Identity as ClaimsIdentity;
                    if (identity != null)
                    {
                        var oid = context.Principal.FindFirst("oid");
                        var nameId = context.Principal.FindFirst(ClaimTypes.NameIdentifier);
                        var sub = context.Principal.FindFirst("sub");
                        
                        // Map oid to NameIdentifier if oid exists but NameIdentifier doesn't
                        if (oid != null && nameId == null)
                        {
                            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, oid.Value));
                            logger?.LogInformation("Mapped oid claim to NameIdentifier: {Value}", oid.Value);
                        }
                        // Map sub to NameIdentifier if neither oid nor NameIdentifier exist
                        else if (sub != null && nameId == null && oid == null)
                        {
                            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub.Value));
                            logger?.LogInformation("Mapped sub claim to NameIdentifier: {Value}", sub.Value);
                        }
                        // If still no NameIdentifier, log warning
                        else if (nameId == null)
                        {
                            logger?.LogError("CRITICAL: Unable to find or create NameIdentifier claim! UserId will be empty.");
                        }
                    }
                    else
                    {
                        logger?.LogError("CRITICAL: Principal.Identity is not a ClaimsIdentity!");
                    }
                },
                OnAuthenticationFailed = async context =>
                {
                    // Call existing handler if present
                    if (existingEvents?.OnAuthenticationFailed != null)
                    {
                        await existingEvents.OnAuthenticationFailed(context);
                    }
                    
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                    logger?.LogError(context.Exception, "Authentication failed during OpenIdConnect");
                },
                OnSignedOutCallbackRedirect = context =>
                {
                    // Call existing handler if present
                    existingEvents?.OnSignedOutCallbackRedirect?.Invoke(context);
                    
                    // Redirect to login page with logout parameter after Azure AD signout
                    context.Response.Redirect("/Account/Login?logout=true");
                    context.HandleResponse();
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            };
        });
    
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
});

// Application Insights
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
    });
}

// DI
builder.Services.AddScoped<ILeaveService, LeaveService>();

// Claims transformation to add roles from database
builder.Services.AddScoped<IClaimsTransformation, DatabaseRoleClaimsTransformation>();

if (builder.Environment.IsProduction())
{
    builder.Services.AddScoped<IFileStore, AzureBlobFileStore>();
}
else
{
    builder.Services.AddSingleton<IFileStore>(sp =>
        new LocalFileStore(configuration.GetValue<string>("LocalFileStore:BasePath") ?? "Data/Uploads", configuration));
}

builder.Services.AddScoped<IEmailService, SendGridEmailService>();

// Logging
builder.Logging.AddConsole();
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddApplicationInsights();
}

var app = builder.Build();

// Auto-apply migrations (or EnsureCreated)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Check if we can connect
        var canConnect = db.Database.CanConnect();
        if (!canConnect)
        {
            logger.LogError("Cannot connect to database. Please check the connection string.");
        }
        else
        {
            logger.LogInformation("Database connection successful.");
            
            // Try to ensure tables are created (this will create them if they don't exist)
            // EnsureCreated respects the ValueGeneratedOnAdd() configuration we set
            logger.LogInformation("Ensuring database tables exist...");
            db.Database.EnsureCreated();
            logger.LogInformation("Database tables verified/created successfully.");
            
            // Try to apply migrations (in case there are pending migrations)
            try
            {
                db.Database.Migrate();
                logger.LogInformation("Migrations applied successfully.");
            }
            catch (Exception migrateEx)
            {
                // Migrations might fail if they're SQLite-specific, that's okay
                logger.LogWarning(migrateEx, "Migration attempt completed (may have failed due to SQLite-specific migrations, but tables should exist).");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed. Please run CreateTables.sql manually in SQL Database Query Editor.");
        logger.LogError("SQL script location: Data/CreateTables.sql");
        // Don't throw - let the app start so user can see the error in logs
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

// Serve uploaded files from Data/Uploads at /uploads
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "Data", "Uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseRouting();

// IMPORTANT: Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map controllers so MicrosoftIdentity UI endpoints are active
app.MapControllers();    // <<< REQUIRED for /MicrosoftIdentity/Account/* routes
app.MapRazorPages();

app.Run();
