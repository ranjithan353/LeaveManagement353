using System.Linq;
using LeaveManagement.Models;
using LeaveManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LeaveManagement.Helpers;
using Microsoft.Extensions.Logging;

namespace LeaveManagement.Pages.Leaves
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ILeaveService _leaveService;
        private readonly IFileStore _fileStore;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(ILeaveService leaveService, IFileStore fileStore, ILogger<CreateModel> logger)
        {
            _leaveService = leaveService;
            _fileStore = fileStore;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? Upload { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Date)]
            public DateTime StartDate { get; set; }

            [Required]
            [DataType(DataType.Date)]
            public DateTime EndDate { get; set; }

            [Required]
            public LeaveManagement.Models.LeaveType? Type { get; set; }

            public string? Reason { get; set; }
        }

        public void OnGet()
        {
            // Pre-populate sensible default dates so the date inputs show today's date
            Input.StartDate = DateTime.Today;
            Input.EndDate = DateTime.Today;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError(string.Empty, "End date must be on or after start date");
                return Page();
            }

            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                // Log all available claims for debugging
                var allClaims = User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
                _logger.LogWarning("UserId is empty. Available claims: {Claims}", string.Join(", ", allClaims));
                ModelState.AddModelError(string.Empty, "Unable to identify user. Please try logging in again.");
                return Page();
            }

            // Log UserId with byte representation
            var userIdBytes = System.Text.Encoding.UTF8.GetBytes(userId);
            _logger.LogInformation("Creating leave request for user '{UserId}' (Length: {Length}, Bytes: [{Bytes}]), StartDate: {StartDate}, EndDate: {EndDate}", 
                userId, userId.Length, string.Join(", ", userIdBytes), Input.StartDate, Input.EndDate);

            try
            {

                var req = new LeaveRequest
                {
                    UserId = userId,
                    StartDate = Input.StartDate,
                    EndDate = Input.EndDate,
                    Type = Input.Type ?? LeaveManagement.Models.LeaveType.Vacation,
                    Reason = Input.Reason
                };

                if (Upload != null && Upload.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream();
                        await Upload.CopyToAsync(ms);
                        // Ensure stream position is at beginning before upload
                        ms.Position = 0;
                        var saved = await _fileStore.SaveFileAsync(ms, Upload.FileName, Upload.ContentType);
                        req.AttachmentUrl = _fileStore.GetPublicUrl(saved);
                        _logger.LogInformation("File uploaded successfully: {FileName}", saved);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "File upload failed");
                        ModelState.AddModelError(nameof(Upload), $"File upload failed: {ex.Message}");
                        return Page();
                    }
                }

                var createdRequest = await _leaveService.CreateAsync(req);
                _logger.LogInformation("Leave request created successfully with ID: {LeaveId} for user {UserId}. UserId stored: '{StoredUserId}'", 
                    createdRequest.Id, userId, createdRequest.UserId);
                
                // Verify the leave was actually saved by querying it back
                try
                {
                    var verifyLeaves = await _leaveService.GetByUserAsync(userId);
                    _logger.LogInformation("Verification: Found {Count} leaves for user {UserId} after creation", verifyLeaves.Count(), userId);
                }
                catch (Exception verifyEx)
                {
                    _logger.LogWarning(verifyEx, "Could not verify leave was saved (non-critical)");
                }
                
                TempData["Toast"] = "Leave request created successfully.";
                return RedirectToPage("/Leaves/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating leave request for user {UserId}", userId);
                
                // Show detailed error message including inner exception
                var errorMessage = $"An error occurred while creating the leave request: {ex.Message}";
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