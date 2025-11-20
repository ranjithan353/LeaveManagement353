using System.Security.Claims;
using LeaveManagement.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Services
{
    /// <summary>
    /// Transforms claims by adding role from EmployeeProfile database table
    /// This allows authorization to work based on database roles, not just Azure AD roles
    /// </summary>
    public class DatabaseRoleClaimsTransformation : IClaimsTransformation
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DatabaseRoleClaimsTransformation> _logger;

        public DatabaseRoleClaimsTransformation(ApplicationDbContext db, ILogger<DatabaseRoleClaimsTransformation> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            // Clone the identity to avoid modifying the original
            var identity = principal.Identity as ClaimsIdentity;
            if (identity == null || !identity.IsAuthenticated)
            {
                return principal;
            }

            // Check if role claim already exists (from Azure AD or previous transformation)
            var existingRoleClaim = principal.FindFirst(ClaimTypes.Role);
            if (existingRoleClaim != null)
            {
                _logger.LogInformation("User already has role claim: {Role}", existingRoleClaim.Value);
            }

            // Get UserId from claims
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                      ?? principal.FindFirst("oid")?.Value 
                      ?? principal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Cannot add database role: UserId not found in claims");
                return principal;
            }

            try
            {
                // Query database for user's profile and role
                var profile = await _db.EmployeeProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile != null && !string.IsNullOrEmpty(profile.Role))
                {
                    // Check if role claim already exists with this value
                    var roleValue = profile.Role.Trim();
                    var existingRole = principal.FindFirst(ClaimTypes.Role)?.Value;

                    if (existingRole != roleValue)
                    {
                        // Remove existing role claim if it's different
                        if (existingRoleClaim != null)
                        {
                            identity.RemoveClaim(existingRoleClaim);
                            _logger.LogInformation("Removed existing role claim: {OldRole}", existingRole);
                        }

                        // Add role claim from database
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                        _logger.LogInformation("Added database role claim: {Role} for UserId: {UserId}", roleValue, userId);
                    }
                    else
                    {
                        _logger.LogInformation("Role claim already matches database role: {Role}", roleValue);
                    }
                }
                else
                {
                    _logger.LogInformation("No profile or role found in database for UserId: {UserId}", userId);
                    
                    // If no role in database and no role claim exists, default to Employee
                    if (existingRoleClaim == null)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, "Employee"));
                        _logger.LogInformation("No role found, defaulting to Employee role");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading role from database for UserId: {UserId}", userId);
                // Don't throw - return principal as-is if database query fails
            }

            return principal;
        }
    }
}

