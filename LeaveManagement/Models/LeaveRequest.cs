using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Models
{
    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class LeaveRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

    [Required]
    public LeaveType Type { get; set; }

    [StringLength(1000)]
    public string? Reason { get; set; }

        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        public string? AttachmentUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}