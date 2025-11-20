using LeaveManagement.Models;
using LeaveManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeaveManagement.Pages.Manager
{
    [Authorize(Roles = "Manager")]
    public class IndexModel : PageModel
    {
        private readonly ILeaveService _leaveService;
        public IndexModel(ILeaveService leaveService)
        {
            _leaveService = leaveService;
        }

        public IEnumerable<LeaveRequest> Pending { get; set; } = Enumerable.Empty<LeaveRequest>();

        public async Task OnGetAsync()
        {
            Pending = await _leaveService.GetPendingAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            await _leaveService.ApproveAsync(id, User.Identity?.Name ?? "");
            TempData["Toast"] = "Leave request approved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            await _leaveService.RejectAsync(id, User.Identity?.Name ?? "");
            TempData["Toast"] = "Leave request rejected.";
            return RedirectToPage();
        }
    }
}