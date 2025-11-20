using LeaveManagement.Data;
using LeaveManagement.Models;
using Microsoft.AspNetCore.Authorization;
using LeaveManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using LeaveManagement.Helpers;
using Microsoft.Extensions.Logging;

namespace LeaveManagement.Pages.Profile
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStore _fileStore;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ApplicationDbContext db, IFileStore fileStore, ILogger<IndexModel> logger)
        {
            _db = db;
            _fileStore = fileStore;
            _logger = logger;
        }

    [BindProperty]
    public EmployeeProfile Profile { get; set; } = new EmployeeProfile();

    [BindProperty]
    public IFormFile? Upload { get; set; }

    // Avatar URL for display (not mapped)
    public string? AvatarUrl { get; set; }

        public async Task OnGetAsync()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                // This shouldn't happen if user is authenticated, but handle gracefully
                return;
            }
            
            var p = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (p == null)
            {
                Profile = new EmployeeProfile { UserId = userId };
            }
            else
            {
                Profile = p;
            }
            if (!string.IsNullOrEmpty(Profile.AvatarFileName))
            {
                AvatarUrl = _fileStore.GetPublicUrl(Profile.AvatarFileName);
                ViewData["AvatarUrl"] = AvatarUrl;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.GetUserId();
            _logger.LogInformation("Profile save attempt - UserId: '{UserId}', IsEmpty: {IsEmpty}", 
                userId ?? "NULL", string.IsNullOrEmpty(userId));
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Profile save failed - UserId is empty");
                ModelState.AddModelError(string.Empty, "Unable to identify user. Please try logging in again.");
                return Page();
            }
            
            // Ensure UserId is set on the bound model before validation
            Profile.UserId = userId;
            _logger.LogInformation("Profile data - UserId: '{UserId}', FullName: '{FullName}', Email: '{Email}'", 
                Profile.UserId, Profile.FullName, Profile.Email);
            
            // Remove any existing ModelState entry for UserId so validation uses the updated value
            ModelState.Remove("Profile.UserId");
            if (!TryValidateModel(Profile))
            {
                _logger.LogWarning("Profile validation failed");
                return Page();
            }

            try
            {
                _logger.LogInformation("Checking for existing profile with UserId: '{UserId}'", userId);
                var existing = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
                _logger.LogInformation("Existing profile found: {Found}, Id: {Id}", existing != null, existing?.Id);
                
                if (existing == null)
                {
                    // Save avatar if provided
                    if (Upload != null && Upload.Length > 0)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            await Upload.CopyToAsync(ms);
                            ms.Position = 0;
                            var saved = await _fileStore.SaveFileAsync(ms, Upload.FileName, Upload.ContentType);
                            Profile.AvatarFileName = saved;
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError(nameof(Upload), $"Failed to upload file: {ex.Message}");
                            return Page();
                        }
                    }
                    _logger.LogInformation("Adding new profile for UserId: '{UserId}'", userId);
                    _db.EmployeeProfiles.Add(Profile);
                }
                else
                {
                    _logger.LogInformation("Updating existing profile Id: {Id} for UserId: '{UserId}'", existing.Id, userId);
                    existing.FullName = Profile.FullName;
                    existing.Email = Profile.Email;
                    existing.Department = Profile.Department;
                    existing.Address = Profile.Address;
                    existing.Phone = Profile.Phone;
                    existing.Role = Profile.Role;
                    existing.HireDate = Profile.HireDate;
                    if (Upload != null && Upload.Length > 0)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            await Upload.CopyToAsync(ms);
                            ms.Position = 0;
                            var saved = await _fileStore.SaveFileAsync(ms, Upload.FileName, Upload.ContentType);
                            existing.AvatarFileName = saved;
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError(nameof(Upload), $"Failed to upload file: {ex.Message}");
                            return Page();
                        }
                    }
                }
                
                _logger.LogInformation("Saving profile changes to database...");
                var savedCount = await _db.SaveChangesAsync();
                _logger.LogInformation("Database save completed. Affected rows: {Count}", savedCount);
                
                TempData["Toast"] = "Profile saved successfully.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                // Log the full exception including inner exception
                _logger.LogError(ex, "Error saving profile for user {UserId}", userId);
                
                // Show detailed error message including inner exception
                var errorMessage = $"An error occurred while saving your profile: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Details: {ex.InnerException.Message}";
                }
                
                // Check for specific database errors
                if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                {
                    if (dbEx.InnerException != null)
                    {
                        errorMessage += $" Database error: {dbEx.InnerException.Message}";
                    }
                }
                
                ModelState.AddModelError(string.Empty, errorMessage);
                return Page();
            }
        }
    }
}
