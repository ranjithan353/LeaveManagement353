using LeaveManagement.Models;

namespace LeaveManagement.Services
{
    public interface ILeaveService
    {
        Task<LeaveRequest> CreateAsync(LeaveRequest request);
        Task<IEnumerable<LeaveRequest>> GetByUserAsync(string userId);
        Task<LeaveRequest?> GetByIdAsync(int id);
        Task<IEnumerable<LeaveRequest>> GetPendingAsync();
        Task<IEnumerable<LeaveRequest>> GetAllLeavesAsync();
        Task ApproveAsync(int id, string managerId);
        Task RejectAsync(int id, string managerId);
    }
}