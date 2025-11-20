using LeaveManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure LeaveRequest
            modelBuilder.Entity<LeaveRequest>(eb =>
            {
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedOnAdd(); // Auto-increment for SQL Server
                eb.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            });
            
            // Configure EmployeeProfile
            modelBuilder.Entity<EmployeeProfile>(eb =>
            {
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedOnAdd(); // Auto-increment for SQL Server
                eb.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.UserId).IsRequired();
            });
        }
    }
}