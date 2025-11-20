using LeaveManagement.Models;
using LeaveManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeaveManagement.Pages.Leaves
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ILeaveService _leaveService;
        public DetailsModel(ILeaveService leaveService)
        {
            _leaveService = leaveService;
        }

        public LeaveRequest Leave { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var l = await _leaveService.GetByIdAsync(id);
            if (l == null) return NotFound();
            Leave = l;
            return Page();
        }
    }
}