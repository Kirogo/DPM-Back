// Controllers/LockController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using geoback.Data;
using geoback.Models;
using geoback.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace geoback.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LockController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LockController> _logger;

    public LockController(ApplicationDbContext context, ILogger<LockController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private string? GetCurrentUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;
    private string? GetCurrentUserName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ??
               $"{User.FindFirst("firstName")?.Value} {User.FindFirst("lastName")?.Value}".Trim();
    }
    private string? GetCurrentUserRole() => User.FindFirst(ClaimTypes.Role)?.Value;

    [HttpPost("acquire")]
    public async Task<ActionResult<LockInfoDto>> AcquireLock([FromBody] LockReportDto dto)
    {
        try
        {
            // Add debug logging
            Console.WriteLine($"========== LOCK ACQUIRE ATTEMPT ==========");
            Console.WriteLine($"ReportId: {dto.ReportId}");
            Console.WriteLine($"UserId from request: {dto.UserId}");
            Console.WriteLine($"SessionId: {dto.SessionId}");
            Console.WriteLine($"UserName: {dto.UserName}");
            Console.WriteLine($"UserRole: {dto.UserRole}");
            
            var userId = GetCurrentUserId();
            var userEmail = GetCurrentUserEmail();
            var userName = GetCurrentUserName() ?? dto.UserName;
            var userRole = GetCurrentUserRole() ?? dto.UserRole;

            Console.WriteLine($"UserId from token: {userId}");
            Console.WriteLine($"UserEmail from token: {userEmail}");
            Console.WriteLine($"UserName from token: {userName}");
            Console.WriteLine($"UserRole from token: {userRole}");

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("❌ AcquireLock: UserId is null or empty from token");
                return Unauthorized(new { message = "User not authenticated - no user ID in token" });
            }

            // Log mismatch but use token user
            if (userId != dto.UserId.ToString())
            {
                Console.WriteLine($"⚠️ UserId mismatch - token has {userId}, request has {dto.UserId}. Using token user.");
                dto.UserId = Guid.Parse(userId);
            }

            Console.WriteLine("✅ User authentication successful");

            // Check for ANY active lock on this report FIRST - use FirstOrDefault with proper filtering
            var existingReportLock = await _context.ReportLocks
                .Where(l => l.ReportId == dto.ReportId && l.IsActive)
                .FirstOrDefaultAsync();

            if (existingReportLock != null)
            {
                Console.WriteLine($"⚠️ Found existing lock: UserId={existingReportLock.UserId}, SessionId={existingReportLock.SessionId}");
                
                // If locked by a DIFFERENT user - return 409 Conflict
                if (existingReportLock.UserId != dto.UserId)
                {
                    Console.WriteLine($"❌ Report locked by DIFFERENT user: {existingReportLock.UserName}");

                    return Conflict(new
                    {
                        message = $"Report is being edited by {existingReportLock.UserName}",
                        code = "LOCKED_BY_OTHER",
                        lockedBy = new
                        {
                            id = existingReportLock.UserId,
                            name = existingReportLock.UserName,
                            role = existingReportLock.UserRole
                        },
                        expiresAt = existingReportLock.ExpiresAt
                    });
                }

                // If locked by SAME user but DIFFERENT session - block
                if (existingReportLock.SessionId != dto.SessionId)
                {
                    Console.WriteLine($"⚠️ User has this report open in another session: {existingReportLock.SessionId} vs {dto.SessionId}");

                    return Conflict(new
                    {
                        message = "You have this report open in another tab. Please use that tab.",
                        code = "LOCKED_IN_OTHER_SESSION"
                    });
                }

                // Same user, same session - refresh the lock
                Console.WriteLine($"🔄 Refreshing existing lock for same session");

                var newExpiry = DateTime.UtcNow.AddMinutes(dto.LockDurationMinutes ?? 5);
                
                existingReportLock.LastHeartbeat = DateTime.UtcNow;
                existingReportLock.ExpiresAt = newExpiry;
                
                var checklist = await _context.Checklists.FindAsync(dto.ReportId);
                if (checklist != null)
                {
                    checklist.LockHeartbeat = DateTime.UtcNow;
                    checklist.LockExpiresAt = newExpiry;
                }

                // Update user active lock
                var userLock = await _context.UserActiveLocks
                    .FirstOrDefaultAsync(u => u.UserId == dto.UserId);
                if (userLock != null)
                {
                    userLock.LastHeartbeat = DateTime.UtcNow;
                    userLock.ExpiresAt = newExpiry;
                }

                await _context.SaveChangesAsync();

                return Ok(new LockInfoDto
                {
                    IsLocked = true,
                    LockedBy = new LockUserDto
                    {
                        Id = dto.UserId,
                        Name = userName,
                        Email = userEmail ?? "",
                        Role = userRole
                    },
                    LockedAt = existingReportLock.LockedAt,
                    ExpiresAt = newExpiry,
                    SessionId = dto.SessionId,
                    IsCurrentUser = true,
                    IsCurrentSession = true,
                    Message = "Lock refreshed successfully"
                });
            }

            // Check if user already has an active lock on ANY report
            var existingUserLock = await _context.UserActiveLocks
                .FirstOrDefaultAsync(u => u.UserId == dto.UserId);

            if (existingUserLock != null && existingUserLock.ReportId != dto.ReportId)
            {
                // User is trying to lock a different report while already having one locked
                var existingReport = await _context.Checklists
                    .Where(c => c.Id == existingUserLock.ReportId)
                    .Select(c => c.DclNo)
                    .FirstOrDefaultAsync();

                Console.WriteLine($"⚠️ User already has lock on different report: {existingReport}");

                return Conflict(new
                {
                    message = $"You already have report {existingReport} open. Please close it first.",
                    code = "USER_HAS_ACTIVE_LOCK",
                    existingLock = new
                    {
                        reportId = existingUserLock.ReportId,
                        reportNo = existingReport,
                        lockedAt = existingUserLock.LockedAt,
                        expiresAt = existingUserLock.ExpiresAt,
                        sessionId = existingUserLock.SessionId
                    }
                });
            }

            // Double-check using raw SQL with correct syntax
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ReportLocks WHERE ReportId = @reportId AND IsActive = 1";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@reportId";
            parameter.Value = dto.ReportId;
            command.Parameters.Add(parameter);
            
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            
            if (count > 0)
            {
                Console.WriteLine($"❌ Lock was created by another user in the meantime (confirmed by raw SQL)");
                return Conflict(new
                {
                    message = "Report was just locked by another user",
                    code = "LOCKED_BY_OTHER"
                });
            }

            // No existing lock - create new one
            Console.WriteLine($"🔒 Creating new lock for report {dto.ReportId}");

            var expiresAt = DateTime.UtcNow.AddMinutes(dto.LockDurationMinutes ?? 5);

            // Create lock record
            var reportLock = new ReportLock
            {
                Id = Guid.NewGuid(),
                ReportId = dto.ReportId,
                UserId = dto.UserId,
                UserEmail = userEmail ?? "",
                UserName = userName,
                UserRole = userRole,
                SessionId = dto.SessionId,
                LockedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true,
                Source = "web"
            };

            // Update checklist
            var report = await _context.Checklists.FindAsync(dto.ReportId);
            if (report == null)
            {
                Console.WriteLine($"❌ Report not found: {dto.ReportId}");
                return NotFound(new { message = "Report not found" });
            }

            report.IsLocked = true;
            report.LockedByUserId = dto.UserId;
            report.LockedByUserName = userName;
            report.LockedByUserRole = userRole;
            report.LockSessionId = dto.SessionId;
            report.LockHeartbeat = DateTime.UtcNow;
            report.LockExpiresAt = expiresAt;
            report.UpdatedAt = DateTime.UtcNow;

            // Update or create user active lock
            if (existingUserLock != null)
            {
                existingUserLock.ReportId = dto.ReportId;
                existingUserLock.SessionId = dto.SessionId;
                existingUserLock.LockedAt = DateTime.UtcNow;
                existingUserLock.LastHeartbeat = DateTime.UtcNow;
                existingUserLock.ExpiresAt = expiresAt;
            }
            else
            {
                var newUserLock = new UserActiveLock
                {
                    UserId = dto.UserId,
                    ReportId = dto.ReportId,
                    SessionId = dto.SessionId,
                    LockedAt = DateTime.UtcNow,
                    LastHeartbeat = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };
                _context.UserActiveLocks.Add(newUserLock);
            }

            _context.ReportLocks.Add(reportLock);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Lock acquired successfully, expires at {expiresAt}");

            return Ok(new LockInfoDto
            {
                IsLocked = true,
                LockedBy = new LockUserDto
                {
                    Id = dto.UserId,
                    Name = userName,
                    Email = userEmail ?? "",
                    Role = userRole
                },
                LockedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                SessionId = dto.SessionId,
                IsCurrentUser = true,
                IsCurrentSession = true,
                Message = "Lock acquired successfully"
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique_active_report") == true)
        {
            // This catches the unique constraint violation - means another user locked it at the exact same moment
            _logger.LogWarning(ex, "Race condition: multiple users tried to lock the same report");
            return Conflict(new
            {
                message = "Report was just locked by another user",
                code = "LOCKED_BY_OTHER"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock");
            Console.WriteLine($"❌ Exception in AcquireLock: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { message = "Error acquiring lock", error = ex.Message });
        }
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult> Heartbeat([FromBody] HeartbeatDto dto)
    {
        try
        {
            Console.WriteLine($"Heartbeat received for report {dto.ReportId}, session {dto.SessionId}");

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"❌ Heartbeat unauthorized: no user in token");
                return Unauthorized();
            }

            var reportLock = await _context.ReportLocks
                .FirstOrDefaultAsync(l => l.ReportId == dto.ReportId 
                    && l.UserId.ToString() == userId
                    && l.SessionId == dto.SessionId
                    && l.IsActive);

            if (reportLock == null)
            {
                Console.WriteLine($"❌ No active lock found for heartbeat");
                return NotFound(new { message = "No active lock found" });
            }

            var newExpiry = DateTime.UtcNow.AddMinutes(5);
            
            reportLock.LastHeartbeat = DateTime.UtcNow;
            reportLock.ExpiresAt = newExpiry;

            var checklist = await _context.Checklists.FindAsync(dto.ReportId);
            if (checklist != null)
            {
                checklist.LockHeartbeat = DateTime.UtcNow;
                checklist.LockExpiresAt = newExpiry;
            }

            var userLock = await _context.UserActiveLocks
                .FirstOrDefaultAsync(u => u.UserId.ToString() == userId);
            if (userLock != null)
            {
                userLock.LastHeartbeat = DateTime.UtcNow;
                userLock.ExpiresAt = newExpiry;
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Heartbeat processed, new expiry {newExpiry}");

            return Ok(new { 
                message = "Heartbeat received", 
                expiresAt = newExpiry 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat");
            return StatusCode(500, new { message = "Error processing heartbeat" });
        }
    }

    [HttpPost("release")]
    public async Task<ActionResult> ReleaseLock([FromBody] UnlockReportDto dto)
    {
        try
        {
            Console.WriteLine($"Release lock request for report {dto.ReportId}, session {dto.SessionId}, reason: {dto.Reason}");

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"❌ Release unauthorized: no user in token");
                return Unauthorized();
            }

            var reportLock = await _context.ReportLocks
                .FirstOrDefaultAsync(l => l.ReportId == dto.ReportId 
                    && l.UserId.ToString() == userId
                    && l.SessionId == dto.SessionId
                    && l.IsActive);

            if (reportLock != null)
            {
                reportLock.IsActive = false;
                Console.WriteLine($"✅ Report lock deactivated");
            }

            var checklist = await _context.Checklists.FindAsync(dto.ReportId);
            if (checklist != null && checklist.LockedByUserId.ToString() == userId)
            {
                checklist.IsLocked = false;
                checklist.LockedByUserId = null;
                checklist.LockedByUserName = null;
                checklist.LockedByUserRole = null;
                checklist.LockSessionId = null;
                checklist.LockHeartbeat = null;
                checklist.LockExpiresAt = null;
                checklist.UpdatedAt = DateTime.UtcNow;
                Console.WriteLine($"✅ Checklist unlocked");
            }

            var userLock = await _context.UserActiveLocks
                .FirstOrDefaultAsync(u => u.UserId.ToString() == userId);
            if (userLock != null && userLock.ReportId == dto.ReportId)
            {
                _context.UserActiveLocks.Remove(userLock);
                Console.WriteLine($"✅ User active lock removed");
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Lock released successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock");
            return StatusCode(500, new { message = "Error releasing lock" });
        }
    }

    [HttpGet("status/{reportId}")]
    public async Task<ActionResult<LockInfoDto>> GetLockStatus(Guid reportId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sessionId = Request.Headers["X-Session-Id"].ToString();

            var reportLock = await _context.ReportLocks
                .Where(l => l.ReportId == reportId && l.IsActive)
                .OrderByDescending(l => l.LockedAt)
                .FirstOrDefaultAsync();

            if (reportLock == null)
            {
                return Ok(new LockInfoDto
                {
                    IsLocked = false,
                    Message = "Report is not locked"
                });
            }

            var isCurrentUser = reportLock.UserId.ToString() == userId;
            var isCurrentSession = isCurrentUser && reportLock.SessionId == sessionId;

            // If lock is expired, clean it up
            if (reportLock.ExpiresAt < DateTime.UtcNow)
            {
                reportLock.IsActive = false;
                
                var checklist = await _context.Checklists.FindAsync(reportId);
                if (checklist != null)
                {
                    checklist.IsLocked = false;
                    checklist.LockedByUserId = null;
                    checklist.LockedByUserName = null;
                    checklist.LockedByUserRole = null;
                    checklist.LockSessionId = null;
                    checklist.LockHeartbeat = null;
                    checklist.LockExpiresAt = null;
                }

                var userLock = await _context.UserActiveLocks
                    .FirstOrDefaultAsync(u => u.UserId == reportLock.UserId);
                if (userLock != null && userLock.ReportId == reportId)
                {
                    _context.UserActiveLocks.Remove(userLock);
                }

                await _context.SaveChangesAsync();

                return Ok(new LockInfoDto
                {
                    IsLocked = false,
                    Message = "Lock expired"
                });
            }

            var user = await _context.Users.FindAsync(reportLock.UserId);

            return Ok(new LockInfoDto
            {
                IsLocked = true,
                LockedBy = new LockUserDto
                {
                    Id = reportLock.UserId,
                    Name = reportLock.UserName,
                    Email = user?.Email ?? reportLock.UserEmail,
                    Role = reportLock.UserRole
                },
                LockedAt = reportLock.LockedAt,
                ExpiresAt = reportLock.ExpiresAt,
                SessionId = reportLock.SessionId,
                IsCurrentUser = isCurrentUser,
                IsCurrentSession = isCurrentSession,
                Message = isCurrentUser 
                    ? (isCurrentSession ? "You have this report open" : "You have this report open in another tab")
                    : $"Report is being edited by {reportLock.UserName}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lock status");
            return StatusCode(500, new { message = "Error getting lock status" });
        }
    }

    [HttpGet("user-active-lock")]
    public async Task<ActionResult<UserActiveLockDto?>> GetUserActiveLock()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var userLock = await _context.UserActiveLocks
                .FirstOrDefaultAsync(u => u.UserId.ToString() == userId);

            if (userLock == null)
                return Ok(null);

            string reportNo = "Unknown";
            try 
            {
                var report = await _context.Checklists
                    .Where(c => c.Id == userLock.ReportId)
                    .Select(c => c.DclNo)
                    .FirstOrDefaultAsync();
                reportNo = report ?? "Unknown";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get report number for lock {ReportId}", userLock.ReportId);
            }

            return Ok(new UserActiveLockDto
            {
                ReportId = userLock.ReportId,
                ReportNo = reportNo,
                LockedAt = userLock.LockedAt,
                ExpiresAt = userLock.ExpiresAt,
                SessionId = userLock.SessionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user active lock");
            return StatusCode(500, new { message = "Error getting user active lock" });
        }
    }

    [HttpPost("force-release/{reportId}")]
    [Authorize(Roles = "Admin,QSManager")]
    public async Task<ActionResult> ForceReleaseLock(Guid reportId)
    {
        try
        {
            var reportLock = await _context.ReportLocks
                .Where(l => l.ReportId == reportId && l.IsActive)
                .FirstOrDefaultAsync();

            if (reportLock != null)
            {
                reportLock.IsActive = false;
            }

            var checklist = await _context.Checklists.FindAsync(reportId);
            if (checklist != null)
            {
                checklist.IsLocked = false;
                checklist.LockedByUserId = null;
                checklist.LockedByUserName = null;
                checklist.LockedByUserRole = null;
                checklist.LockSessionId = null;
                checklist.LockHeartbeat = null;
                checklist.LockExpiresAt = null;
                checklist.UpdatedAt = DateTime.UtcNow;
            }

            var userLock = await _context.UserActiveLocks
                .FirstOrDefaultAsync(u => u.ReportId == reportId);
            if (userLock != null)
            {
                _context.UserActiveLocks.Remove(userLock);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Lock force-released successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force-releasing lock");
            return StatusCode(500, new { message = "Error force-releasing lock" });
        }
    }
}