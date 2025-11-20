using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeaveManagement.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly ILogger<LoginModel> _logger;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public LoginModel(ILogger<LoginModel> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public IActionResult OnGet()
        {
            // Check if this is after logout (prevent auto-login)
            if (Request.Query.ContainsKey("logout") || Request.Query.ContainsKey("signedout"))
            {
                return Page(); // Show login page without triggering challenge
            }

            // If already logged in → Go to Dashboard
            if (User?.Identity?.IsAuthenticated ?? false)
                return LocalRedirect("/Dashboard");

            // In PRODUCTION → trigger Azure AD Login
            if (_env.IsProduction())
            {
                var props = new AuthenticationProperties
                {
                    RedirectUri = "/Dashboard", // <== Redirect here after login
                    // Force account selection
                    Items = { { "prompt", "select_account" } }
                };

                return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
            }

            // In DEVELOPMENT → show username/password login
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Block POST login in Production
            if (_env.IsProduction())
            {
                var props = new AuthenticationProperties
                {
                    RedirectUri = "/Dashboard",
                    // Force account selection
                    Items = { { "prompt", "select_account" } }
                };

                return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
            }

            // LOCAL DEV LOGIN
            var users = new[] {
                new { Username = "user", Password = "password", Name = "Employee User", Role = "Employee", Id = "user-oid-123" },
                new { Username = "manager", Password = "password", Name = "Manager User", Role = "Manager", Id = "manager-oid-123" },
                new { Username = "admin", Password = "password", Name = "Admin User", Role = "Admin", Id = "admin-oid-123" }
            };

            var u = users.FirstOrDefault(x => x.Username == Username && x.Password == Password);

            if (u == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, u.Id),
                new Claim(ClaimTypes.Name, u.Name),
                new Claim(ClaimTypes.Role, u.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return LocalRedirect("/Dashboard");
        }
    }
}
