using LeaveManagement.Data;
using LeaveManagement.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace LeaveManagement.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;
        private readonly ILogger<LeaveService> _logger;

        public LeaveService(ApplicationDbContext db, IEmailService emailService, ILogger<LeaveService> logger)
        {
            _db = db;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<LeaveRequest> CreateAsync(LeaveRequest request)
        {
            if (request.EndDate < request.StartDate)
                throw new ArgumentException("EndDate must be >= StartDate");

            if (string.IsNullOrEmpty(request.UserId))
            {
                _logger.LogError("Attempted to create leave request with empty UserId");
                throw new ArgumentException("UserId cannot be empty");
            }

            // Log UserId with byte representation to catch hidden characters
            var userIdBytes = System.Text.Encoding.UTF8.GetBytes(request.UserId);
            _logger.LogInformation("Creating leave request for UserId: '{UserId}' (Length: {Length}, Bytes: [{Bytes}]), Type: {Type}, StartDate: {StartDate}, EndDate: {EndDate}", 
                request.UserId, request.UserId.Length, string.Join(", ", userIdBytes), request.Type, request.StartDate, request.EndDate);

            // Ensure UserId is trimmed before saving to avoid matching issues
            var originalUserId = request.UserId;
            request.UserId = request.UserId.Trim();
            
            if (originalUserId != request.UserId)
            {
                _logger.LogInformation("UserId trimmed: '{OriginalUserId}' -> '{TrimmedUserId}'", originalUserId, request.UserId);
            }
            
            request.CreatedAt = DateTime.UtcNow;
            request.Status = LeaveStatus.Pending;
            _db.LeaveRequests.Add(request);
            
            try
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("Leave request saved successfully with ID: {Id}, UserId: '{UserId}'", request.Id, request.UserId);
                
                // Immediately verify the leave was saved by querying it back
                try
                {
                    var savedLeave = await _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == request.Id);
                    if (savedLeave != null)
                    {
                        var savedUserIdBytes = System.Text.Encoding.UTF8.GetBytes(savedLeave.UserId ?? "");
                        _logger.LogInformation("Verification: Leave {Id} found in database with UserId: '{UserId}' (Length: {Length}, Bytes: [{Bytes}])", 
                            savedLeave.Id, savedLeave.UserId, savedLeave.UserId?.Length ?? 0, string.Join(", ", savedUserIdBytes));
                        
                        // Verify UserId matches
                        if (savedLeave.UserId != request.UserId)
                        {
                            _logger.LogWarning("WARNING: Saved UserId '{SavedUserId}' does not match original '{OriginalUserId}'", 
                                savedLeave.UserId, request.UserId);
                        }
                    }
                    else
                    {
                        _logger.LogError("ERROR: Leave {Id} was not found in database after save!", request.Id);
                    }
                }
                catch (Exception verifyEx)
                {
                    _logger.LogWarning(verifyEx, "Could not verify leave was saved (non-critical)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save leave request to database. UserId: {UserId}", request.UserId);
                throw;
            }
            
            return request;
        }

        public async Task<IEnumerable<LeaveRequest>> GetAllLeavesAsync()
        {
            try
            {
                return await LoadAllLeavesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all leaves");
                return Enumerable.Empty<LeaveRequest>();
            }
        }

        /// <summary>
        /// Loads all leaves from database using direct SQL connection to bypass EF Core mapping issues
        /// </summary>
        private async Task<List<LeaveRequest>> LoadAllLeavesAsync()
        {
            var connection = _db.Database.GetDbConnection();
            await connection.OpenAsync();
            
            List<LeaveRequest> allLeaves = new List<LeaveRequest>();
            
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        [Id],
                        [UserId],
                        [StartDate],
                        [EndDate],
                        [Type],
                        [Reason],
                        [Status],
                        [AttachmentUrl],
                        [CreatedAt]
                    FROM [LeaveRequests]
                    ORDER BY [CreatedAt] DESC";
                
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    try
                    {
                        // Manually map the results, handling Id as string and converting to int
                        var idValue = reader["Id"];
                        int id;
                        
                        if (idValue is int intId)
                        {
                            id = intId;
                        }
                        else if (idValue is string stringId && int.TryParse(stringId, out int parsedId))
                        {
                            id = parsedId;
                        }
                        else
                        {
                            _logger.LogWarning("Could not parse Id value: {IdValue}, type: {Type}", idValue, idValue?.GetType().Name);
                            continue; // Skip this row
                        }
                        
                        var leave = new LeaveRequest
                        {
                            Id = id,
                            UserId = reader["UserId"]?.ToString() ?? string.Empty,
                            StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")),
                            EndDate = reader.GetDateTime(reader.GetOrdinal("EndDate")),
                            Type = Enum.Parse<LeaveType>(reader["Type"]?.ToString() ?? "Vacation"),
                            Reason = reader["Reason"]?.ToString(),
                            Status = Enum.Parse<LeaveStatus>(reader["Status"]?.ToString() ?? "Pending"),
                            AttachmentUrl = reader["AttachmentUrl"]?.ToString(),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                        };
                        
                        allLeaves.Add(leave);
                    }
                    catch (Exception mapEx)
                    {
                        _logger.LogWarning(mapEx, "Error mapping leave row, skipping. Id: {Id}", reader["Id"]);
                    }
                }
                
                _logger.LogInformation("Loaded {Count} leaves from database", allLeaves.Count);
            }
            finally
            {
                await connection.CloseAsync();
            }
            
            return allLeaves;
        }

        public async Task<IEnumerable<LeaveRequest>> GetByUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetByUserAsync called with empty UserId");
                return Enumerable.Empty<LeaveRequest>();
            }

            var trimmedUserId = userId.Trim();
            
            // Log UserId with byte representation to catch hidden characters
            var userIdBytes = System.Text.Encoding.UTF8.GetBytes(trimmedUserId);
            _logger.LogInformation("Querying leaves for UserId: '{UserId}' (Length: {Length}, Bytes: [{Bytes}])", 
                trimmedUserId, trimmedUserId.Length, string.Join(", ", userIdBytes));
            
            try
            {
                // Use helper method to load all leaves
                var allLeaves = await LoadAllLeavesAsync();
                
                var totalCount = allLeaves.Count;
                _logger.LogInformation("Loaded {TotalCount} total leaves from database", totalCount);
                
                // If no leaves at all, return empty
                if (totalCount == 0)
                {
                    _logger.LogWarning("No leaves found in database at all");
                    return Enumerable.Empty<LeaveRequest>();
                }
                
                // Log all UserIds in database for debugging
                var allUserIds = allLeaves.Select(r => r.UserId).Distinct().ToList();
                var userIdDetails = allUserIds.Select(id => 
                {
                    if (id == null) return "NULL";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(id);
                    return $"'{id}' (len:{id.Length})";
                });
                _logger.LogInformation("UserIds in database: {UserIds}", string.Join(", ", userIdDetails));
                
                // Try multiple matching strategies
                var leaves = new List<LeaveRequest>();
                
                // Strategy 1: Exact match (trimmed)
                leaves = allLeaves
                    .Where(r => r.UserId != null && r.UserId.Trim() == trimmedUserId)
                    .ToList();
                
                _logger.LogInformation("Strategy 1 (exact trimmed match): Found {Count} leaves", leaves.Count);
                
                // Strategy 2: Case-insensitive match (if no results)
                if (leaves.Count == 0)
                {
                    var lowerTrimmedUserId = trimmedUserId.ToLowerInvariant();
                    leaves = allLeaves
                        .Where(r => r.UserId != null && r.UserId.Trim().ToLowerInvariant() == lowerTrimmedUserId)
                        .ToList();
                    
                    _logger.LogInformation("Strategy 2 (case-insensitive match): Found {Count} leaves", leaves.Count);
                }
                
                // Strategy 3: Contains match (partial match for debugging)
                if (leaves.Count == 0)
                {
                    var lowerTrimmedUserId = trimmedUserId.ToLowerInvariant();
                    leaves = allLeaves
                        .Where(r => r.UserId != null && 
                            (r.UserId.Trim().ToLowerInvariant().Contains(lowerTrimmedUserId) || 
                             lowerTrimmedUserId.Contains(r.UserId.Trim().ToLowerInvariant())))
                        .ToList();
                    
                    if (leaves.Count > 0)
                    {
                        _logger.LogWarning("Strategy 3 (contains match): Found {Count} leaves with similar UserIds", leaves.Count);
                        var similarUserIds = leaves.Select(l => l.UserId).Distinct().ToList();
                        _logger.LogWarning("Similar UserIds found: {UserIds}", string.Join(", ", similarUserIds));
                    }
                }
                
                if (leaves.Count > 0)
                {
                    // Log the UserIds of found leaves for verification
                    var foundUserIds = leaves.Select(l => l.UserId).Distinct().ToList();
                    _logger.LogInformation("Successfully found {Count} leaves with UserIds: {UserIds}", 
                        leaves.Count, string.Join(", ", foundUserIds));
                }
                else
                {
                    _logger.LogWarning("No leaves found for UserId '{UserId}' after trying all strategies", trimmedUserId);
                    _logger.LogWarning("Searching for: '{SearchUserId}' (len:{Length}, bytes:[{Bytes}])", 
                        trimmedUserId, trimmedUserId.Length, string.Join(",", userIdBytes));
                }
                
                return leaves;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying leaves for UserId: '{UserId}'", trimmedUserId);
                return Enumerable.Empty<LeaveRequest>();
            }
        }

        public async Task<LeaveRequest?> GetByIdAsync(int id)
        {
            try
            {
                // Use helper method to load all leaves, then find by id
                var allLeaves = await LoadAllLeavesAsync();
                return allLeaves.FirstOrDefault(r => r.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leave by ID: {Id}", id);
                return null;
            }
        }

        public async Task<IEnumerable<LeaveRequest>> GetPendingAsync()
        {
            try
            {
                // Use helper method to load all leaves, then filter for pending
                var allLeaves = await LoadAllLeavesAsync();
                return allLeaves.Where(r => r.Status == LeaveStatus.Pending).OrderBy(r => r.CreatedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending leaves");
                return Enumerable.Empty<LeaveRequest>();
            }
        }

        public async Task ApproveAsync(int id, string managerId)
        {
            _logger.LogInformation("Approving leave request ID: {Id} by manager: {ManagerId}", id, managerId);
            
            // Use direct SQL UPDATE to bypass EF Core tracking issues
            // This ensures the update works regardless of Id column type
            try
            {
                var connection = _db.Database.GetDbConnection();
                await connection.OpenAsync();
                
                try
                {
                    // First, verify the leave exists and is pending
                    var req = await GetByIdAsync(id);
                    if (req == null)
                    {
                        throw new InvalidOperationException($"Leave request with ID {id} not found");
                    }
                    if (req.Status != LeaveStatus.Pending)
                    {
                        throw new InvalidOperationException($"Leave request {id} is already {req.Status}, cannot approve");
                    }
                    
                    using var command = connection.CreateCommand();
                    // Use parameterized query to handle both string and int Id columns
                    command.CommandText = @"
                        UPDATE [LeaveRequests]
                        SET [Status] = @Status
                        WHERE [Id] = @Id 
                          AND [Status] = @PendingStatus";
                    
                    var idParam = command.CreateParameter();
                    idParam.ParameterName = "@Id";
                    idParam.Value = id;
                    command.Parameters.Add(idParam);
                    
                    var statusParam = command.CreateParameter();
                    statusParam.ParameterName = "@Status";
                    statusParam.Value = "Approved";
                    command.Parameters.Add(statusParam);
                    
                    var pendingParam = command.CreateParameter();
                    pendingParam.ParameterName = "@PendingStatus";
                    pendingParam.Value = "Pending";
                    command.Parameters.Add(pendingParam);
                    
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    
                    if (rowsAffected == 0)
                    {
                        throw new InvalidOperationException($"Failed to update leave request {id}. It may have been modified by another user.");
                    }
                    
                    _logger.LogInformation("Successfully approved leave request ID: {Id}. Rows affected: {Rows}", id, rowsAffected);
                    
                    // Send notification using the original request data
                    await _emailService.SendLeaveStatusNotificationAsync(
                        req.UserId, 
                        req.UserId, 
                        req.Type.ToString(), 
                        req.StartDate, 
                        req.EndDate, 
                        "Approved");
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving leave request ID: {Id}", id);
                throw;
            }
        }

        public async Task RejectAsync(int id, string managerId)
        {
            _logger.LogInformation("Rejecting leave request ID: {Id} by manager: {ManagerId}", id, managerId);
            
            // Use direct SQL UPDATE to bypass EF Core tracking issues
            // This ensures the update works regardless of Id column type
            try
            {
                var connection = _db.Database.GetDbConnection();
                await connection.OpenAsync();
                
                try
                {
                    // First, verify the leave exists and is pending
                    var req = await GetByIdAsync(id);
                    if (req == null)
                    {
                        throw new InvalidOperationException($"Leave request with ID {id} not found");
                    }
                    if (req.Status != LeaveStatus.Pending)
                    {
                        throw new InvalidOperationException($"Leave request {id} is already {req.Status}, cannot reject");
                    }
                    
                    using var command = connection.CreateCommand();
                    // Use parameterized query to handle both string and int Id columns
                    command.CommandText = @"
                        UPDATE [LeaveRequests]
                        SET [Status] = @Status
                        WHERE [Id] = @Id 
                          AND [Status] = @PendingStatus";
                    
                    var idParam = command.CreateParameter();
                    idParam.ParameterName = "@Id";
                    idParam.Value = id;
                    command.Parameters.Add(idParam);
                    
                    var statusParam = command.CreateParameter();
                    statusParam.ParameterName = "@Status";
                    statusParam.Value = "Rejected";
                    command.Parameters.Add(statusParam);
                    
                    var pendingParam = command.CreateParameter();
                    pendingParam.ParameterName = "@PendingStatus";
                    pendingParam.Value = "Pending";
                    command.Parameters.Add(pendingParam);
                    
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    
                    if (rowsAffected == 0)
                    {
                        throw new InvalidOperationException($"Failed to update leave request {id}. It may have been modified by another user.");
                    }
                    
                    _logger.LogInformation("Successfully rejected leave request ID: {Id}. Rows affected: {Rows}", id, rowsAffected);
                    
                    // Send notification using the original request data
                    await _emailService.SendLeaveStatusNotificationAsync(
                        req.UserId, 
                        req.UserId, 
                        req.Type.ToString(), 
                        req.StartDate, 
                        req.EndDate, 
                        "Rejected");
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting leave request ID: {Id}", id);
                throw;
            }
        }
    }
}