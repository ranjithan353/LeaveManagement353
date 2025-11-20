using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Models
{
    public class EmployeeProfile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // oid / identifier

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public string? Department { get; set; }

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public string? Role { get; set; }

        public string? AvatarFileName { get; set; }

        [DataType(DataType.Date)]
        public DateTime? HireDate { get; set; }
    }
}
