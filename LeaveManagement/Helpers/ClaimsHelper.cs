using System.Security.Claims;

namespace LeaveManagement.Helpers
{
    public static class ClaimsHelper
    {
        /// <summary>
        /// Gets the user ID from claims. Checks both NameIdentifier (standard) and oid (Azure AD) claims.
        /// </summary>
        public static string GetUserId(this ClaimsPrincipal user)
        {
            // Try NameIdentifier first (standard claim)
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // If not found, try oid (Azure AD object identifier)
            if (string.IsNullOrEmpty(userId))
            {
                userId = user.FindFirst("oid")?.Value;
            }
            
            // If still not found, try sub (subject identifier)
            if (string.IsNullOrEmpty(userId))
            {
                userId = user.FindFirst("sub")?.Value;
            }
            
            return userId ?? string.Empty;
        }
    }
}

