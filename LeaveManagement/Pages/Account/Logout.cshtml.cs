using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace LeaveManagement.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly IWebHostEnvironment _env;

        public LogoutModel(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Sign out of local cookies if present
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (_env.IsProduction())
            {
                // Sign out of Azure AD and redirect to Azure AD logout endpoint
                var callbackUrl = Url.Page("/Account/Login", pageHandler: null, values: new { logout = "true" }, protocol: Request.Scheme);
                
                return SignOut(
                    new AuthenticationProperties { RedirectUri = callbackUrl },
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    OpenIdConnectDefaults.AuthenticationScheme);
            }
            else
            {
                // In development, just sign out and redirect
                return LocalRedirect("/Account/Login?logout=true");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (_env.IsProduction())
            {
                var callbackUrl = Url.Page("/Account/Login", pageHandler: null, values: new { logout = "true" }, protocol: Request.Scheme);
                
                return SignOut(
                    new AuthenticationProperties { RedirectUri = callbackUrl },
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    OpenIdConnectDefaults.AuthenticationScheme);
            }
            else
            {
                return LocalRedirect("/Account/Login?logout=true");
            }
        }
    }
}
