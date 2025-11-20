using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LeaveManagement.Data;
using LeaveManagement.Models;
using LeaveManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LeaveManagement.Tests
{
    public class LeaveServiceTests
    {
        private ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private LeaveService CreateLeaveService(ApplicationDbContext db)
        {
            var mockEmailService = new Mock<IEmailService>();
            var mockLogger = new Mock<ILogger<LeaveService>>();
            return new LeaveService(db, mockEmailService.Object, mockLogger.Object);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenEndBeforeStart()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            var req = new LeaveRequest { UserId = "u1", StartDate = DateTime.Today.AddDays(5), EndDate = DateTime.Today, Type = LeaveType.Vacation };
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(req));
        }

        [Fact]
        public async Task CreateAsync_ShouldSucceed_WhenValidRequest()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            var req = new LeaveRequest 
            { 
                UserId = "u1", 
                StartDate = DateTime.Today, 
                EndDate = DateTime.Today.AddDays(3), 
                Type = LeaveType.Vacation,
                Reason = "Annual leave"
            };
            var result = await service.CreateAsync(req);
            
            Assert.NotNull(result);
            Assert.Equal(LeaveStatus.Pending, result.Status);
            Assert.NotEqual(0, result.Id);
        }

        [Fact]
        public async Task Approve_ChangesStatus()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            var req = new LeaveRequest { UserId = "u1", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Sick };
            await service.CreateAsync(req);
            await service.ApproveAsync(req.Id, "manager");
            var saved = await service.GetByIdAsync(req.Id);
            Assert.Equal(LeaveStatus.Approved, saved!.Status);
        }

        [Fact]
        public async Task Reject_ChangesStatus()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            var req = new LeaveRequest { UserId = "u1", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Sick };
            await service.CreateAsync(req);
            await service.RejectAsync(req.Id, "manager");
            var saved = await service.GetByIdAsync(req.Id);
            Assert.Equal(LeaveStatus.Rejected, saved!.Status);
        }

        [Fact]
        public async Task GetByUserAsync_ReturnsOnlyUserRequests()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            
            var req1 = new LeaveRequest { UserId = "user1", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Vacation };
            var req2 = new LeaveRequest { UserId = "user2", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Sick };
            
            await service.CreateAsync(req1);
            await service.CreateAsync(req2);
            
            var user1Requests = (await service.GetByUserAsync("user1")).ToList();
            
            Assert.Single(user1Requests);
            Assert.Equal("user1", user1Requests[0].UserId);
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsPendingRequestsOnly()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            
            var req1 = new LeaveRequest { UserId = "user1", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Vacation };
            var req2 = new LeaveRequest { UserId = "user2", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Sick };
            
            await service.CreateAsync(req1);
            await service.CreateAsync(req2);
            
            // Approve first one
            await service.ApproveAsync(req1.Id, "manager");
            
            var pendingRequests = (await service.GetPendingAsync()).ToList();
            
            Assert.Single(pendingRequests);
            Assert.Equal(LeaveStatus.Pending, pendingRequests[0].Status);
        }

        [Fact]
        public async Task ApproveAsync_ShouldThrow_WhenRequestNotFound()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(999, "manager"));
        }

        [Fact]
        public async Task ApproveAsync_ShouldThrow_WhenAlreadyApproved()
        {
            using var db = CreateDbContext();
            var service = CreateLeaveService(db);
            
            var req = new LeaveRequest { UserId = "user1", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1), Type = LeaveType.Vacation };
            await service.CreateAsync(req);
            await service.ApproveAsync(req.Id, "manager");
            
            // Try to approve again
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(req.Id, "manager"));
        }
    }
}