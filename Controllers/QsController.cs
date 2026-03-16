// Controllers/QsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using geoback.Data;
using geoback.Models;
using geoback.DTOs;
using System.Text.Json;
using System.Security.Claims;

namespace geoback.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public QsController(ApplicationDbContext context, ILogger<QsController> logger)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private async Task<(string? userId, string? userName, string? userRole)> GetCurrentUserInfo()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return (null, null, null);

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
            return (userId, null, null);

        var userName = $"{user.FirstName} {user.LastName}".Trim();
        var userRole = user.Role;

        return (userId, userName, userRole);
    }

    [HttpGet("dashboard/stats")]
    public async Task<ActionResult<object>> GetDashboardStats()
    {
        try
        {
            var userId = GetCurrentUserId();

            var stats = new
            {
                PendingReviews = await _context.Checklists
                    .CountAsync(c => c.Status == "submitted" || c.Status == "pending_qs_review"),
                InProgress = await _context.Checklists
                    .CountAsync(c => c.Status == "under_review"),
                CompletedToday = await _context.Checklists
                    .CountAsync(c => c.Status == "approved" &&
                        c.UpdatedAt.Date == DateTime.UtcNow.Date),
                ScheduledVisits = await _context.Checklists
                    .CountAsync(c => c.Status == "site_visit_scheduled"),
                AverageResponseTime = await CalculateAverageResponseTime(),
                CriticalIssues = await _context.Checklists
                    .CountAsync(c => c.Priority == "High" || c.Priority == "Critical"),
                MyActiveReviews = await _context.Checklists
                    .CountAsync(c => c.AssignedToQS == userId && c.Status == "under_review"),
                OverdueReviews = await _context.Checklists
                    .CountAsync(c => c.Status == "under_review" &&
                        c.UpdatedAt < DateTime.UtcNow.AddDays(-2))
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return StatusCode(500, new { message = "Error fetching dashboard statistics" });
        }
    }

    private async Task<string> CalculateAverageResponseTime()
    {
        var approvedReports = await _context.Checklists
            .Where(c => c.Status == "approved" && c.SubmittedAt != null && c.ReviewedAt != null)
            .ToListAsync();

        if (!approvedReports.Any())
            return "0h";

        var totalHours = approvedReports
            .Select(c => (c.ReviewedAt!.Value - c.SubmittedAt!.Value).TotalHours)
            .Average();

        return $"{Math.Round(totalHours)}h";
    }

    [HttpGet("reviews/pending")]
    public async Task<ActionResult<object>> GetPendingReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = _context.Checklists
                .Where(c => c.Status == "submitted" || c.Status == "pending_qs_review")
                .OrderByDescending(c => c.SubmittedAt ?? c.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reportDtos = items.Select(c => MapToReportDto(c)).ToList();

            return Ok(new
            {
                items = reportDtos,
                total = total,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending reviews");
            return StatusCode(500, new { message = "Error fetching pending reviews" });
        }
    }

    [HttpGet("reviews/in-progress")]
    public async Task<ActionResult<object>> GetInProgressReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();

            var query = _context.Checklists
                .Where(c => c.Status == "rework" && c.AssignedToQS == userId)
                .OrderByDescending(c => c.UpdatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reportDtos = items.Select(c => MapToReportDto(c)).ToList();

            return Ok(new
            {
                items = reportDtos,
                total = total,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting in-progress reviews");
            return StatusCode(500, new { message = "Error fetching in-progress reviews" });
        }
    }

    [HttpGet("reviews/completed")]
    public async Task<ActionResult<object>> GetCompletedReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();

            var query = _context.Checklists
                .Where(c => c.Status == "approved" && c.AssignedToQS == userId)
                .OrderByDescending(c => c.ReviewedAt ?? c.UpdatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reportDtos = items.Select(c => MapToReportDto(c)).ToList();

            return Ok(new
            {
                items = reportDtos,
                total = total,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed reviews");
            return StatusCode(500, new { message = "Error fetching completed reviews" });
        }
    }

    [HttpGet("reviews/{id}")]
    public async Task<ActionResult<object>> GetReportDetails(Guid id)
    {
        try
        {
            var report = await _context.Checklists
                .FirstOrDefaultAsync(c => c.Id == id);

            if (report == null)
                return NotFound(new { message = $"Report with ID {id} not found" });

            return Ok(MapToReportDto(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting report details: {ReportId}", id);
            return StatusCode(500, new { message = "Error fetching report details" });
        }
    }

    [HttpGet("reviews/{id}/comments")]
    public async Task<ActionResult<List<CommentDto>>> GetReportComments(Guid id)
    {
        try
        {
            var comments = await _context.Comments
                .Where(c => c.ReportId == id)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    ReportId = c.ReportId,
                    UserId = c.UserId,
                    UserName = c.UserName,
                    UserRole = c.UserRole,
                    Text = c.Text,
                    IsInternal = c.IsInternal,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();

            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for report: {ReportId}", id);
            return StatusCode(500, new { message = "Error fetching comments" });
        }
    }

    [HttpPost("reviews/{id}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(Guid id, [FromBody] CreateCommentDto dto)
    {
        try
        {
            var (userId, userName, userRole) = await GetCurrentUserInfo();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var finalUserName = !string.IsNullOrEmpty(userName) ? userName : "QS User";
            var finalUserRole = !string.IsNullOrEmpty(userRole) ? userRole : "QS";

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                ReportId = id,
                UserId = Guid.Parse(userId),
                UserName = finalUserName,
                UserRole = finalUserRole,
                Text = dto.Comment,
                IsInternal = dto.IsInternal,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var commentDto = new CommentDto
            {
                Id = comment.Id,
                ReportId = comment.ReportId,
                UserId = comment.UserId,
                UserName = comment.UserName,
                UserRole = comment.UserRole,
                Text = comment.Text,
                IsInternal = comment.IsInternal,
                CreatedAt = comment.CreatedAt
            };

            return Ok(commentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to report: {ReportId}", id);
            return StatusCode(500, new { message = "Error adding comment" });
        }
    }

    [HttpPost("reviews/{id}/assign")]
    public async Task<IActionResult> AssignToMe(Guid id)
    {
        try
        {
            var (userId, userName, _) = await GetCurrentUserInfo();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var report = await _context.Checklists.FindAsync(id);
            if (report == null)
                return NotFound(new { message = $"Report with ID {id} not found" });

            report.AssignedToQS = userId;
            report.AssignedToQSName = userName;
            report.Status = "under_review";
            report.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Report assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning report: {ReportId}", id);
            return StatusCode(500, new { message = "Error assigning report" });
        }
    }

    [HttpPost("reviews/{id}/revision")]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] RevisionRequestDto dto)
    {
        try
        {
            var (userId, userName, userRole) = await GetCurrentUserInfo();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var report = await _context.Checklists.FindAsync(id);
            if (report == null)
                return NotFound(new { message = $"Report with ID {id} not found" });

            _logger.LogInformation($"RequestRevision called for report {id}. Current status: {report.Status}");

            report.Status = "rework";
            report.UpdatedAt = DateTime.UtcNow;

            var finalUserName = !string.IsNullOrEmpty(userName) ? userName : "QS User";
            var finalUserRole = !string.IsNullOrEmpty(userRole) ? userRole : "QS";

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                ReportId = id,
                UserId = Guid.Parse(userId),
                UserName = finalUserName,
                UserRole = finalUserRole,
                Text = dto.Notes,
                IsInternal = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);

            await _context.SaveChangesAsync();
            _logger.LogInformation($"SaveChangesAsync result: {report.Status}");

            return Ok(new { message = "Revision requested successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting revision for report: {ReportId}", id);
            return StatusCode(500, new { message = "Error requesting revision" });
        }
    }

    [HttpPost("reviews/{id}/schedule-site-visit")]
    public async Task<IActionResult> ScheduleSiteVisit(Guid id, [FromBody] ScheduleSiteVisitDto dto)
    {
        try
        {
            var (userId, userName, userRole) = await GetCurrentUserInfo();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var report = await _context.Checklists.FindAsync(id);
            if (report == null)
                return NotFound(new { message = $"Report with ID {id} not found" });

            // Check if report is already assigned to another QS
            if (!string.IsNullOrEmpty(report.AssignedToQS) && report.AssignedToQS != userId)
            {
                return BadRequest(new { message = "This report is already assigned to another QS" });
            }

            _logger.LogInformation($"ScheduleSiteVisit called for report {id}. Current status: {report.Status}");

            // Update report status
            report.Status = "site_visit_scheduled";
            report.AssignedToQS = userId;
            report.AssignedToQSName = userName;
            report.UpdatedAt = DateTime.UtcNow;

            // Store site visit details
            report.SiteVisitScheduledDate = dto.ScheduledDate;
            report.SiteVisitNotes = dto.Notes;
            report.SiteVisitScheduledBy = userId;
            report.SiteVisitScheduledByName = userName;

            // Add system comment
            var finalUserName = !string.IsNullOrEmpty(userName) ? userName : "QS User";
            var finalUserRole = !string.IsNullOrEmpty(userRole) ? userRole : "QS";

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                ReportId = id,
                UserId = Guid.Parse(userId),
                UserName = finalUserName,
                UserRole = finalUserRole,
                Text = $"SITE VISIT SCHEDULED for {dto.ScheduledDate:yyyy-MM-dd HH:mm}. Notes: {dto.Notes}",
                IsInternal = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Site visit scheduled for report {id}. New status: {report.Status}");

            return Ok(new
            {
                message = "Site visit scheduled successfully",
                scheduledDate = dto.ScheduledDate,
                status = report.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling site visit for report: {ReportId}", id);
            return StatusCode(500, new { message = "Error scheduling site visit" });
        }
    }

    [HttpPost("reviews/{id}/confirm-site-visit")]
    public async Task<IActionResult> ConfirmSiteVisit(Guid id, [FromBody] ConfirmSiteVisitDto dto)
    {
        try
        {
            var (userId, userName, userRole) = await GetCurrentUserInfo();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var report = await _context.Checklists.FindAsync(id);
            if (report == null)
                return NotFound(new { message = $"Report with ID {id} not found" });

            // Verify this QS scheduled the visit
            if (report.AssignedToQS != userId)
            {
                return BadRequest(new { message = "Only the QS who scheduled this visit can confirm it" });
            }

            _logger.LogInformation($"ConfirmSiteVisit called for report {id}. Current status: {report.Status}");

            // Add site visit findings comment
            var finalUserName = !string.IsNullOrEmpty(userName) ? userName : "QS User";
            var finalUserRole = !string.IsNullOrEmpty(userRole) ? userRole : "QS";

            var findingsComment = new Comment
            {
                Id = Guid.NewGuid(),
                ReportId = id,
                UserId = Guid.Parse(userId),
                UserName = finalUserName,
                UserRole = finalUserRole,
                Text = $"SITE VISIT FINDINGS:\n{dto.Findings}",
                IsInternal = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(findingsComment);

            // Store the findings
            report.SiteVisitFindings = dto.Findings;
            report.SiteVisitConfirmedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Site visit confirmed for report {id}");

            return Ok(new
            {
                message = "Site visit confirmed successfully",
                findings = dto.Findings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming site visit for report: {ReportId}", id);
            return StatusCode(500, new { message = "Error confirming site visit" });
        }
    }

    [HttpGet("reviews/site-visits")]
    public async Task<ActionResult<object>> GetSiteVisits([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = _context.Checklists
                .Where(c => c.Status == "site_visit_scheduled")
                .OrderBy(c => c.SiteVisitScheduledDate ?? c.UpdatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reportDtos = items.Select(c => MapToReportDto(c)).ToList();

            return Ok(new
            {
                items = reportDtos,
                total = total,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting site visits");
            return StatusCode(500, new { message = "Error fetching site visits" });
        }
    }

    [HttpGet("reviews/site-visits/stats")]
    public async Task<ActionResult<SiteVisitStatsDto>> GetSiteVisitsStats()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var total = await _context.Checklists
                .CountAsync(c => c.Status == "site_visit_scheduled");

            var todayCount = await _context.Checklists
                .CountAsync(c => c.Status == "site_visit_scheduled" &&
                    c.SiteVisitScheduledDate.HasValue &&
                    c.SiteVisitScheduledDate.Value.Date == today);

            var upcomingCount = await _context.Checklists
                .CountAsync(c => c.Status == "site_visit_scheduled" &&
                    c.SiteVisitScheduledDate.HasValue &&
                    c.SiteVisitScheduledDate.Value.Date > today);

            return Ok(new SiteVisitStatsDto
            {
                Total = total,
                Today = todayCount,
                Upcoming = upcomingCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting site visits stats");
            return StatusCode(500, new { message = "Error fetching site visits statistics" });
        }
    }

    [HttpPost("reviews/{id}/approve")]
    public async Task<IActionResult> ApproveReport(Guid id, [FromBody] ApproveReportDto? dto)
    {
        try
        {
            var (userId, userName, userRole) = await GetCurrentUserInfo();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var report = await _context.Checklists.FindAsync(id);
            if (report == null)
                return NotFound(new { message = $"Report with ID {id} not found" });

            report.Status = "approved";
            report.ReviewedAt = DateTime.UtcNow;
            report.ReviewedBy = userId;
            report.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(dto?.Notes))
            {
                var finalUserName = !string.IsNullOrEmpty(userName) ? userName : "QS User";
                var finalUserRole = !string.IsNullOrEmpty(userRole) ? userRole : "QS";

                var comment = new Comment
                {
                    Id = Guid.NewGuid(),
                    ReportId = id,
                    UserId = Guid.Parse(userId),
                    UserName = finalUserName,
                    UserRole = finalUserRole,
                    Text = dto.Notes,
                    IsInternal = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Comments.Add(comment);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Report approved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving report: {ReportId}", id);
            return StatusCode(500, new { message = "Error approving report" });
        }
    }

    private object MapToReportDto(Checklist checklist)
    {
        object? siteVisitForm = null;
        if (!string.IsNullOrWhiteSpace(checklist.SiteVisitFormJson) && checklist.SiteVisitFormJson != "null")
        {
            try
            {
                siteVisitForm = JsonSerializer.Deserialize<object>(checklist.SiteVisitFormJson);
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        var documents = new List<object>();
        if (!string.IsNullOrWhiteSpace(checklist.DocumentsJson) && checklist.DocumentsJson != "[]")
        {
            try
            {
                documents = JsonSerializer.Deserialize<List<object>>(checklist.DocumentsJson) ?? new List<object>();
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        return new
        {
            id = checklist.Id,
            reportNo = checklist.DclNo,
            customerId = checklist.CustomerId,
            customerNumber = checklist.CustomerNumber,
            customerName = checklist.CustomerName,
            customerEmail = checklist.CustomerEmail,
            projectName = checklist.LoanType,
            loanType = checklist.LoanType,
            ibpsNo = checklist.IbpsNo,
            status = checklist.Status,
            rmId = checklist.AssignedToRM,
            rmName = GetRmName(checklist.AssignedToRM).Result,
            documents = documents,
            siteVisitForm = siteVisitForm,
            isLocked = checklist.IsLocked,
            lockedBy = checklist.LockedByUserId.HasValue ? new
            {
                id = checklist.LockedByUserId,
                name = checklist.LockedByUserName,
                role = checklist.LockedByUserRole
            } : null,
            lockedAt = checklist.LockedAt,
            assignedToQS = checklist.AssignedToQS,
            assignedToQSName = checklist.AssignedToQSName,
            submittedAt = checklist.SubmittedAt,
            priority = checklist.Priority,
            reviewedAt = checklist.ReviewedAt,
            reviewedBy = checklist.ReviewedBy,
            createdAt = checklist.CreatedAt,
            updatedAt = checklist.UpdatedAt,
            // Site Visit properties
            siteVisitScheduledDate = checklist.SiteVisitScheduledDate,
            siteVisitNotes = checklist.SiteVisitNotes,
            siteVisitFindings = checklist.SiteVisitFindings,
            siteVisitConfirmedAt = checklist.SiteVisitConfirmedAt,
            siteVisitScheduledBy = checklist.SiteVisitScheduledBy,
            siteVisitScheduledByName = checklist.SiteVisitScheduledByName
        };
    }

    private async Task<string?> GetRmName(Guid? rmId)
    {
        if (!rmId.HasValue) return null;

        var user = await _context.Users.FindAsync(rmId.Value);
        return user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
    }
}