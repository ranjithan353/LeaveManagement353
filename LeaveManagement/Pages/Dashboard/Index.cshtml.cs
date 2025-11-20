using System.Linq;
using LeaveManagement.Data;
using LeaveManagement.Models;
using LeaveManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LeaveManagement.Helpers;
using Microsoft.Extensions.Logging;

namespace LeaveManagement.Pages.Dashboard
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ILeaveService _leaveService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ApplicationDbContext db, ILeaveService leaveService, ILogger<IndexModel> logger)
        {
            _db = db;
            _leaveService = leaveService;
            _logger = logger;
        }

    public int TotalLeaves { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public bool IsManager { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsEmployee { get; set; }
    public int TotalUsers { get; set; }
    public int ManagersCount { get; set; }
    public int EmployeesCount { get; set; }
    public IEnumerable<LeaveRequest> Recent { get; set; } = Enumerable.Empty<LeaveRequest>();

        public async Task OnGetAsync()
        {
            try
            {
                var userId = User.GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    // Log all available claims for debugging
                    var allClaims = User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
                    _logger.LogWarning("UserId is empty when loading dashboard. Available claims: {Claims}", string.Join(", ", allClaims));
                }
                else
                {
                    // Log UserId with byte representation
                    var userIdBytes = System.Text.Encoding.UTF8.GetBytes(userId);
                    _logger.LogInformation("Loading dashboard for user '{UserId}' (Length: {Length}, Bytes: [{Bytes}])", 
                        userId, userId.Length, string.Join(", ", userIdBytes));
                }
                
                IsManager = User.IsInRole("Manager");
                IsAdmin = User.IsInRole("Admin");
                IsEmployee = !IsManager && !IsAdmin;

                // Initialize defaults
                TotalLeaves = 0;
                Approved = 0;
                Rejected = 0;
                Pending = 0;
                TotalUsers = 0;
                ManagersCount = 0;
                EmployeesCount = 0;
                Recent = Enumerable.Empty<LeaveRequest>();

                // load avatar for current user if present
                try
                {
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                        if (profile != null && !string.IsNullOrEmpty(profile.AvatarFileName))
                        {
                            var fs = HttpContext.RequestServices.GetService(typeof(LeaveManagement.Services.IFileStore)) as LeaveManagement.Services.IFileStore;
                            if (fs != null)
                            {
                                try
                                {
                                    ViewData["AvatarUrl"] = fs.GetPublicUrl(profile.AvatarFileName);
                                }
                                catch (Exception avatarEx)
                                {
                                    _logger.LogWarning(avatarEx, "Failed to generate avatar URL");
                                }
                            }
                        }
                    }
                }
                catch (Exception profileEx)
                {
                    _logger.LogWarning(profileEx, "Error loading profile for avatar");
                }

                // Load dashboard data based on role
                if (IsAdmin)
                {
                    try
                    {
                        // Admin overview
                        TotalUsers = await _db.EmployeeProfiles.CountAsync();
                        ManagersCount = await _db.EmployeeProfiles.CountAsync(p => p.Role != null && p.Role.ToLower().Contains("manager"));
                        EmployeesCount = TotalUsers - ManagersCount;
                        
                        // Load all leaves using LeaveService (handles Id column type mismatch)
                        var allLeaves = await _leaveService.GetAllLeavesAsync();
                        var allLeavesList = allLeaves.ToList();
                        
                        TotalLeaves = allLeavesList.Count;
                        Approved = allLeavesList.Count(r => r.Status == LeaveStatus.Approved);
                        Rejected = allLeavesList.Count(r => r.Status == LeaveStatus.Rejected);
                        Pending = allLeavesList.Count(r => r.Status == LeaveStatus.Pending);
                        Recent = allLeavesList.OrderByDescending(r => r.CreatedAt).Take(20);
                    }
                    catch (Exception adminEx)
                    {
                        _logger.LogError(adminEx, "Error loading admin dashboard data");
                    }
                }
                else if (IsManager)
                {
                    try
                    {
                        // Manager sees team/global overview with pending focus
                        // Load all leaves using LeaveService (handles Id column type mismatch)
                        var allLeaves = await _leaveService.GetAllLeavesAsync();
                        var allLeavesList = allLeaves.ToList();
                        
                        TotalLeaves = allLeavesList.Count;
                        Approved = allLeavesList.Count(r => r.Status == LeaveStatus.Approved);
                        Rejected = allLeavesList.Count(r => r.Status == LeaveStatus.Rejected);
                        Pending = allLeavesList.Count(r => r.Status == LeaveStatus.Pending);
                        Recent = allLeavesList.Where(r => r.Status == LeaveStatus.Pending).OrderBy(r => r.StartDate).Take(20);
                    }
                    catch (Exception managerEx)
                    {
                        _logger.LogError(managerEx, "Error loading manager dashboard data");
                    }
                }
                else // IsEmployee
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(userId))
                        {
                            _logger.LogInformation("Dashboard: Loading employee leaves for UserId: '{UserId}'", userId);
                            
                            // Use LeaveService for consistency with My Leaves page
                            var employeeLeaves = await _leaveService.GetByUserAsync(userId);
                            var leavesList = employeeLeaves.ToList();
                            
                            TotalLeaves = leavesList.Count;
                            Approved = leavesList.Count(r => r.Status == LeaveStatus.Approved);
                            Rejected = leavesList.Count(r => r.Status == LeaveStatus.Rejected);
                            Pending = leavesList.Count(r => r.Status == LeaveStatus.Pending);
                            Recent = leavesList.OrderByDescending(r => r.CreatedAt).Take(10);
                            
                            _logger.LogInformation("Dashboard: Found {Count} total leaves for user {UserId}", TotalLeaves, userId);
                        }
                    }
                    catch (Exception employeeEx)
                    {
                        _logger.LogError(employeeEx, "Error loading employee dashboard data for user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error loading dashboard");
                // Set safe defaults so page still renders
                TotalLeaves = 0;
                Approved = 0;
                Rejected = 0;
                Pending = 0;
                Recent = Enumerable.Empty<LeaveRequest>();
            }
        }
    }
}
