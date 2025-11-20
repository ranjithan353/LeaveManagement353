using System.Linq;
using LeaveManagement.Models;
using LeaveManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using LeaveManagement.Helpers;
using Microsoft.Extensions.Logging;

namespace LeaveManagement.Pages.Leaves
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ILeaveService _leaveService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILeaveService leaveService, ILogger<IndexModel> logger)
        {
            _leaveService = leaveService;
            _logger = logger;
        }

        public IEnumerable<LeaveRequest> Leaves { get; set; } = Enumerable.Empty<LeaveRequest>();

        public async Task OnGetAsync()
        {
            try
            {
                var userId = User.GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    // Log all available claims for debugging
                    var allClaims = User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
                    _logger.LogWarning("UserId is empty when loading leaves. Available claims: {Claims}", string.Join(", ", allClaims));
                    Leaves = Enumerable.Empty<LeaveRequest>();
                    return;
                }

                // Log UserId with byte representation
                var userIdBytes = System.Text.Encoding.UTF8.GetBytes(userId);
                _logger.LogInformation("Loading leaves for user '{UserId}' (Length: {Length}, Bytes: [{Bytes}])", 
                    userId, userId.Length, string.Join(", ", userIdBytes));
                Leaves = await _leaveService.GetByUserAsync(userId);
                _logger.LogInformation("Loaded {Count} leave requests for user {UserId}", Leaves.Count(), userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading leave requests");
                Leaves = Enumerable.Empty<LeaveRequest>();
                // Don't throw - show empty list instead of error page
            }
        }
    }
}